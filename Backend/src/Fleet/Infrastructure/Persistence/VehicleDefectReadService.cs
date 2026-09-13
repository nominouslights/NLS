using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Inspections;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// Derives a vehicle's defect backlog from <c>fleet.rm_vehicle_inspections</c>. There is no
/// defect table to query: a defect is a jsonb element inside its DVIR, so this is a five-step
/// derivation over three round trips — the unit number, the vehicle's inspections, and the
/// work-order context — followed by an in-memory fan-out.
///
/// Tenant scoping is inherited from the DbContext's <c>HasQueryFilter</c> plus the tables' RLS
/// policies. There is deliberately no manual tenant predicate here and no
/// <c>IgnoreQueryFilters</c> anywhere: adding either would duplicate one half of the dual
/// enforcement and quietly weaken the other.
/// </summary>
internal sealed class VehicleDefectReadService(FleetDbContext context) : IVehicleDefectReadService
{
    public async Task<IReadOnlyList<VehicleDefectResponse>> GetDefectsForVehicleAsync(
        Guid vehicleId,
        bool includeResolved,
        CancellationToken cancellationToken = default)
    {
        // 1. In DB — the unit number, so legacy unit-only DVIRs can be matched below. An unknown
        //    or disposed vehicle yields null here and the query falls back to the id predicate
        //    alone, which returns nothing: an empty list, not a 404 and not a throw.
        var unit = await context.VehicleReadModels
            .AsNoTracking()
            .Where(v => v.Id == vehicleId)
            .Select(v => v.UnitNumber)
            .FirstOrDefaultAsync(cancellationToken);

        // 2. In DB — this vehicle's inspections. Resolved defects are ALWAYS fetched, even when
        //    includeResolved is false, because step 5's recurrence derivation needs the cleared
        //    history to look back at.
        var query = context.VehicleInspectionReadModels.AsNoTracking();

        query = unit is null
            ? query.Where(i => i.VehicleId == vehicleId)
            : query.Where(i => i.VehicleId == vehicleId || (i.VehicleId == null && i.Unit == unit));

        var inspections = await query
            .OrderByDescending(i => i.PerformedAt)
            .ToListAsync(cancellationToken);

        // 3. In memory — narrow to the DVIRs that actually reported something. This cannot be a
        //    SQL predicate: Defects is ToJson, so the parent row has to materialize before the
        //    collection exists. GetInspectionsAsync lives under the same constraint.
        var reporting = inspections.Where(i => i.Defects.Count > 0).ToList();
        if (reporting.Count == 0)
        {
            return [];
        }

        // 4. In DB — the work-order context, as an id-scoped lookup rather than a SQL JOIN:
        //    GeneratedWorkOrderId is a bare id with no navigation property (and no FK).
        var workOrdersById = await LoadWorkOrdersAsync(reporting, cancellationToken);

        // 5. In memory — fan out one row per defect, derive recurrence, filter, order.
        var cleared = reporting
            .SelectMany(i => i.Defects.Where(d => d.IsResolved).Select(d => (Inspection: i, Defect: d)))
            .ToList();

        var rows = new List<(InspectionDefectSeverity Severity, DateTimeOffset ReportedAt, VehicleDefectResponse Row)>();

        foreach (var inspection in reporting)
        {
            foreach (var defect in inspection.Defects)
            {
                if (defect.IsResolved && !includeResolved)
                {
                    continue;
                }

                var workOrder = inspection.GeneratedWorkOrderId is { } workOrderId
                    && workOrdersById.TryGetValue(workOrderId, out var found)
                        ? found
                        : default;

                rows.Add((
                    defect.Severity,
                    inspection.PerformedAt,
                    new VehicleDefectResponse(
                        inspection.Id,
                        inspection.VehicleId,
                        inspection.Unit,
                        inspection.Type,
                        inspection.TripNumber,
                        inspection.DriverName,
                        inspection.PerformedAt,
                        defect.Item,
                        defect.Severity.ToString(),
                        defect.Note,
                        // The id is carried even when the row is missing (fail loud, not silent):
                        // number and status simply come back null.
                        inspection.GeneratedWorkOrderId,
                        workOrder.Number,
                        workOrder.Status,
                        defect.ResolutionReason?.ToString(),
                        defect.ResolutionNote,
                        defect.ResolvedBy,
                        defect.ResolvedAtUtc,
                        defect.ResolvedByWorkOrderId,
                        DeriveRecurrence(inspection, defect, cleared, workOrdersById))));
            }
        }

        // OutOfService → Major → Minor, newest first within each. Pre-ordered here rather than on
        // each surface, so the four places that render this can never disagree about one truck.
        return rows
            .OrderBy(r => SeverityRank(r.Severity))
            .ThenByDescending(r => r.ReportedAt)
            .Select(r => r.Row)
            .ToList();
    }

    private async Task<Dictionary<Guid, (string? Number, string? Status)>> LoadWorkOrdersAsync(
        IReadOnlyList<VehicleInspectionReadModel> reporting,
        CancellationToken cancellationToken)
    {
        var generatedIds = reporting
            .Where(i => i.GeneratedWorkOrderId is not null)
            .Select(i => i.GeneratedWorkOrderId!.Value)
            .Distinct()
            .ToList();

        var byId = new Dictionary<Guid, (string? Number, string? Status)>();
        if (generatedIds.Count == 0)
        {
            return byId;
        }

        var workOrders = await context.WorkOrderReadModels
            .AsNoTracking()
            .Where(w => generatedIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Number, w.Status })
            .ToListAsync(cancellationToken);

        foreach (var workOrder in workOrders)
        {
            byId[workOrder.Id] = (workOrder.Number, workOrder.Status);
        }

        return byId;
    }

    /// <summary>
    /// "This fault has come back": the most recent earlier resolution of the same item on the
    /// same vehicle. Match is trimmed and case-insensitive, and <c>PerformedAt</c> must be
    /// strictly earlier. Resolved rows get no recurrence — the citation belongs on the open
    /// defect that supersedes the cleared one, not on the cleared one itself.
    /// </summary>
    private static PreviousResolutionResponse? DeriveRecurrence(
        VehicleInspectionReadModel inspection,
        InspectionDefect defect,
        IReadOnlyList<(VehicleInspectionReadModel Inspection, InspectionDefect Defect)> cleared,
        IReadOnlyDictionary<Guid, (string? Number, string? Status)> workOrdersById)
    {
        if (defect.IsResolved)
        {
            return null;
        }

        var item = defect.Item.Trim();

        var previous = cleared
            .Where(c => c.Inspection.PerformedAt < inspection.PerformedAt
                && string.Equals(c.Defect.Item.Trim(), item, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.Inspection.PerformedAt)
            .FirstOrDefault();

        if (previous.Inspection is null)
        {
            return null;
        }

        var workOrderNumber = previous.Defect.ResolvedByWorkOrderId is { } workOrderId
            && workOrdersById.TryGetValue(workOrderId, out var found)
                ? found.Number
                : null;

        return new PreviousResolutionResponse(
            previous.Inspection.Id,
            previous.Inspection.PerformedAt,
            previous.Defect.ResolvedAtUtc!.Value,
            previous.Defect.ResolutionReason?.ToString() ?? string.Empty,
            workOrderNumber,
            Explicit: defect.RecurrenceOfInspectionId is not null);
    }

    private static int SeverityRank(InspectionDefectSeverity severity) => severity switch
    {
        InspectionDefectSeverity.OutOfService => 0,
        InspectionDefectSeverity.Major => 1,
        _ => 2,
    };
}
