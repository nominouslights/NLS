using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Services;
using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Application.WorkOrders.Complete;

/// <summary>
/// Logs the service record that resolved the work order and closes it — and applies each defect
/// line's outcome to the inspection it came from. Completion is the only point that asserts a
/// mechanic actually touched the truck: creating or starting a work order shows as "repair
/// underway" on the defects panel but leaves every defect open. Completion never changes the
/// vehicle's status; returning it to service is a separate, deliberate action.
///
/// Two paths:
/// <list type="bullet">
/// <item>WITH defect lines — Repaired resolves the defect as RepairedUnderWorkOrder, NoFaultFound
/// as NoFaultFound (both attributed to the service record's PerformedBy and this work order);
/// Deferred releases it, still open, for a later work order. An inspection or item that has since
/// gone (removed, or amended away) is skipped — the outcome is still recorded on the line.</item>
/// <item>WITHOUT lines (manual, or created before per-defect links) — the interim #106 behaviour,
/// unchanged: resolve the defects of the generating inspection that the line items name.</item>
/// </list>
/// </summary>
public sealed class CompleteWorkOrderCommandHandler(
    IWorkOrderRepository workOrderRepository,
    IServiceRecordRepository serviceRepository,
    IVehicleInspectionRepository inspectionRepository)
    : ICommandHandler<CompleteWorkOrderCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CompleteWorkOrderCommand command, CancellationToken cancellationToken)
    {
        var workOrder = await workOrderRepository.GetByIdAsync(command.WorkOrderId, cancellationToken);
        if (workOrder is null)
        {
            return Result.Failure<Guid>(WorkOrderErrors.NotFound);
        }

        if (workOrder.IsTerminal)
        {
            return Result.Failure<Guid>(WorkOrderErrors.Terminal);
        }

        var sequence = await serviceRepository.NextSequenceAsync(command.TenantId, cancellationToken);
        var number = $"SVC-{sequence}";

        var parts = command.PartsUsed
            .Where(p => !string.IsNullOrWhiteSpace(p.Sku))
            .Select(p => new ServicePart { Sku = p.Sku.Trim(), Qty = p.Qty <= 0 ? 1 : p.Qty })
            .ToList();

        var serviceResult = ServiceRecord.Log(
            command.TenantId,
            workOrder.VehicleId,
            number,
            command.Date,
            command.PerformedBy,
            command.Category,
            command.OdometerKm,
            command.ItemsChanged,
            command.Reason,
            parts,
            command.LaborHours,
            command.CostCad,
            workOrder.Id,
            command.Notes);

        if (serviceResult.IsFailure)
        {
            return Result.Failure<Guid>(serviceResult.Error);
        }

        var service = serviceResult.Value;

        // Complete BEFORE the service record is added, so an outcome validation failure leaves
        // nothing tracked at all.
        var completeResult = workOrder.Complete(service.Id, command.DefectOutcomes ?? []);
        if (completeResult.IsFailure)
        {
            return Result.Failure<Guid>(completeResult.Error);
        }

        serviceRepository.Add(service);

        var now = DateTimeOffset.UtcNow;

        if (workOrder.HasDefectLines)
        {
            await ApplyOutcomesAsync(workOrder, service.PerformedBy, now, cancellationToken);
        }
        else
        {
            await ResolveLegacyAsync(workOrder, command.PerformedBy, now, cancellationToken);
        }

        // All aggregates live on the same scoped DbContext — one save commits together.
        await workOrderRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(service.Id);
    }

    private async Task ApplyOutcomesAsync(
        WorkOrder workOrder,
        string resolvedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var inspections = new Dictionary<Guid, VehicleInspection?>();

        foreach (var line in workOrder.Defects)
        {
            if (!inspections.TryGetValue(line.InspectionId, out var inspection))
            {
                inspection = await inspectionRepository.GetByIdAsync(line.InspectionId, cancellationToken);
                inspections[line.InspectionId] = inspection;
            }

            // Removed since the work order was raised: the line keeps its outcome as the record
            // of what the mechanic did; there is simply no defect left to update.
            if (inspection is null)
            {
                continue;
            }

            // Each method is a no-op when the item is gone or no longer on this work order.
            switch (line.Outcome)
            {
                case DefectRepairOutcome.Repaired:
                    inspection.ResolveDefectUnderWorkOrder(
                        line.Item, workOrder.Id, DefectResolutionReason.RepairedUnderWorkOrder,
                        line.OutcomeNote, resolvedBy, now);
                    break;

                case DefectRepairOutcome.NoFaultFound:
                    inspection.ResolveDefectUnderWorkOrder(
                        line.Item, workOrder.Id, DefectResolutionReason.NoFaultFound,
                        line.OutcomeNote, resolvedBy, now);
                    break;

                case DefectRepairOutcome.Deferred:
                    inspection.ReleaseDefectFromWorkOrder(line.Item, workOrder.Id);
                    break;
            }
        }
    }

    /// <summary>
    /// The #106 interim path for work orders with no defect lines: resolve only the defects of the
    /// generating inspection that the free-text line items name (null coverage = unrecognisable
    /// line items = every open defect). No source inspection is the normal case for a directly
    /// raised work order, not an error. Already-resolved defects are skipped inside the aggregate.
    /// </summary>
    private async Task ResolveLegacyAsync(
        WorkOrder workOrder,
        string performedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sourceInspection = await inspectionRepository.GetByGeneratedWorkOrderIdAsync(
            workOrder.Id, cancellationToken);

        sourceInspection?.ResolveDefectsForWorkOrder(
            workOrder.Id,
            performedBy,
            now,
            sourceInspection.DefectItemsCoveredBy(workOrder.LineItems));
    }
}
