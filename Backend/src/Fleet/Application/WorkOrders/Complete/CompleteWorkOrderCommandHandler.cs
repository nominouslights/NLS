using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Inspections;
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
/// Deferred releases it, still open, for a later work order. Neither removing the inspection nor
/// amending the item away is allowed while the defect is on this (open) work order — both refuse
/// with <c>Fleet.Inspection.DefectOnActiveWorkOrder</c> — but an inspection or item that is
/// missing anyway is skipped defensively, and the outcome is still recorded on the line. The
/// inspections are loaded BEFORE the work order completes, so the "OutOfService is never
/// Deferred" rule is judged against each defect's current severity, not the line's snapshot.</item>
/// <item>WITHOUT lines (manual, or created before per-defect links) — the interim #106 behaviour,
/// unchanged: resolve the defects of the generating inspection that the line items name.</item>
/// </list>
/// Both paths attribute the resolution to the service record's PerformedBy (trimmed), so the
/// defect history and the service log always name the same person.
///
/// Two phases. Phase 1 is everything above — the service record, the work order's completion and
/// each line's own outcome — committed in one save, exactly as before duplicates existed; every
/// failure mode of the call is phase 1's.
///
/// Phase 2 then clears DUPLICATES, server-decided (there is no preview for a completion), in a
/// separate save that retries version conflicts and otherwise leaves them open — it can never
/// fail the call (<see cref="DuplicateDefectCascade"/>). For every defect this completion
/// actually REPAIRED (a Repaired line, or the legacy path's RepairedUnderWorkOrder), the same
/// component's still-open defects on the work order's vehicle are resolved with that stamp —
/// <see cref="DuplicateDefects"/> — but only:
/// <list type="bullet">
/// <item>NoFaultFound and Deferred never cascade: they are judgements about one report
/// (<see cref="DefectResolutionReasonExtensions.ClearsDuplicates"/>);</item>
/// <item>a defect on ANY line of this work order is never touched by the cascade, whatever that
/// line's outcome — each line's outcome is authoritative for its own defect, so a Deferred
/// sibling stays open;</item>
/// <item>only reports made strictly before the START of the service date (in the operating
/// zone) are cleared — a same-day report may postdate the repair, so it stays open
/// (<see cref="DefectRepairCutoff.ForServiceDate"/>);</item>
/// <item>the scope is computed once from the work order's vehicle and the candidates for every
/// repaired item come from one query.</item>
/// </list>
/// A line whose defect had already been cleared by hand only detaches, and cascades nothing.
/// </summary>
public sealed class CompleteWorkOrderCommandHandler(
    IWorkOrderRepository workOrderRepository,
    IServiceRecordRepository serviceRepository,
    IVehicleInspectionRepository inspectionRepository,
    DuplicateDefectCascade duplicates)
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

        // Loaded up front: Complete needs each defect's CURRENT severity, and the outcomes are
        // then applied to these same instances. Null = the inspection has since been removed.
        var inspections = workOrder.HasDefectLines
            ? await LoadInspectionsAsync(workOrder, cancellationToken)
            : [];

        // Complete BEFORE the service record is added, so an outcome validation failure leaves
        // nothing tracked at all.
        var completeResult = workOrder.Complete(
            service.Id,
            command.DefectOutcomes ?? [],
            line => inspections.GetValueOrDefault(line.InspectionId)?.FindDefect(line.Item)?.Severity);
        if (completeResult.IsFailure)
        {
            return Result.Failure<Guid>(completeResult.Error);
        }

        serviceRepository.Add(service);

        var now = DateTimeOffset.UtcNow;

        // One normalized attribution for both paths — the service record's (trimmed) PerformedBy.
        var resolvedBy = service.PerformedBy;

        var resolvedHere = workOrder.HasDefectLines
            ? ApplyOutcomes(workOrder, inspections, resolvedBy, now)
            : await ResolveLegacyAsync(workOrder, resolvedBy, now, cancellationToken);

        // ---- Phase 1: all aggregates live on the same scoped DbContext — one save commits together. ----
        await workOrderRepository.SaveChangesAsync(cancellationToken);

        // ---- Phase 2: duplicates of what this work order repaired; never fails the call. ----
        if (resolvedHere.Count > 0)
        {
            await duplicates.ClearForVehicleAsync(
                workOrder.VehicleId,
                resolvedHere,
                DefectRepairCutoff.ForServiceDate(service.Date, now),
                candidate => !workOrder.Defects.Any(line => line.Addresses(candidate.Inspection.Id, candidate.Defect.Item)),
                cancellationToken);
        }

        return Result.Success(service.Id);
    }

    /// <summary>Each distinct inspection the work order's lines point at, by id — null when it has since been removed.</summary>
    private async Task<Dictionary<Guid, VehicleInspection?>> LoadInspectionsAsync(
        WorkOrder workOrder,
        CancellationToken cancellationToken)
    {
        var inspections = new Dictionary<Guid, VehicleInspection?>();

        foreach (var inspectionId in workOrder.Defects.Select(l => l.InspectionId).Distinct())
        {
            inspections[inspectionId] = await inspectionRepository.GetByIdAsync(inspectionId, cancellationToken);
        }

        return inspections;
    }

    /// <summary>Applies each line's outcome to its own defect; returns the defects actually resolved here.</summary>
    private static List<DefectOnInspection> ApplyOutcomes(
        WorkOrder workOrder,
        IReadOnlyDictionary<Guid, VehicleInspection?> inspections,
        string resolvedBy,
        DateTimeOffset now)
    {
        var resolved = new List<DefectOnInspection>();

        foreach (var line in workOrder.Defects)
        {
            var inspection = inspections.GetValueOrDefault(line.InspectionId);

            // Removed since the work order was raised: the line keeps its outcome as the record
            // of what the mechanic did; there is simply no defect left to update.
            if (inspection is null)
            {
                continue;
            }

            // Each method is a no-op when the item is gone or no longer on this work order.
            var reason = line.Outcome switch
            {
                DefectRepairOutcome.Repaired => DefectResolutionReason.RepairedUnderWorkOrder,
                DefectRepairOutcome.NoFaultFound => DefectResolutionReason.NoFaultFound,
                _ => (DefectResolutionReason?)null,
            };

            if (reason is { } resolution)
            {
                if (inspection.ResolveDefectUnderWorkOrder(
                    line.Item, workOrder.Id, resolution, line.OutcomeNote, resolvedBy, now))
                {
                    resolved.Add(new DefectOnInspection(inspection, inspection.FindDefect(line.Item)!));
                }
            }
            else if (line.Outcome == DefectRepairOutcome.Deferred)
            {
                inspection.ReleaseDefectFromWorkOrder(line.Item, workOrder.Id);
            }
        }

        return resolved;
    }

    /// <summary>
    /// The #106 interim path for work orders with no defect lines: resolve only the defects of the
    /// generating inspection that the free-text line items name (null coverage = unrecognisable
    /// line items = every open defect). No source inspection is the normal case for a directly
    /// raised work order, not an error. Already-resolved defects are skipped inside the aggregate.
    /// Returns the defects actually resolved here.
    /// </summary>
    private async Task<List<DefectOnInspection>> ResolveLegacyAsync(
        WorkOrder workOrder,
        string performedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sourceInspection = await inspectionRepository.GetByGeneratedWorkOrderIdAsync(
            workOrder.Id, cancellationToken);

        if (sourceInspection is null)
        {
            return [];
        }

        var resolved = sourceInspection.ResolveDefectsForWorkOrder(
            workOrder.Id,
            performedBy,
            now,
            sourceInspection.DefectItemsCoveredBy(workOrder.LineItems));

        return [.. resolved.Select(d => new DefectOnInspection(sourceInspection, d))];
    }
}
