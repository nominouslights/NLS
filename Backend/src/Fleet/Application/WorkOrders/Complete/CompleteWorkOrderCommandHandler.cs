using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Services;
using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Application.WorkOrders.Complete;

/// <summary>
/// Logs the service record that resolved the work order and closes it — and, when the work order
/// came from a DVIR, clears that DVIR's defects. Completion is the only point that asserts a
/// mechanic actually touched the truck: creating or starting a work order shows as "repair
/// underway" on the defects panel but leaves every defect open.
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
        serviceRepository.Add(service);

        var completeResult = workOrder.Complete(service.Id);
        if (completeResult.IsFailure)
        {
            return Result.Failure<Guid>(completeResult.Error);
        }

        // The repair is now evidence, so the defects it was raised against stop being open. No
        // source inspection is the normal case for a directly-raised work order, not an error.
        // Already-resolved defects are skipped inside the aggregate, so this is idempotent.
        var sourceInspection = await inspectionRepository.GetByGeneratedWorkOrderIdAsync(
            workOrder.Id, cancellationToken);

        sourceInspection?.ResolveDefectsForWorkOrder(
            workOrder.Id, command.PerformedBy, DateTimeOffset.UtcNow);

        // All three aggregates live on the same scoped DbContext — one save commits together,
        // the same pattern CreateWorkOrderCommandHandler uses for LinkWorkOrder.
        await workOrderRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(service.Id);
    }
}
