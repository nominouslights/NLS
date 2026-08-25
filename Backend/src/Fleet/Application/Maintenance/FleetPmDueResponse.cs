namespace NorthernLink.Fleet.Application.Maintenance;

/// <summary>
/// The fleet-wide PM dashboard package: one row per assigned, non-disposed vehicle, each
/// carrying its DueSoon/Overdue/NotYetRecorded counts and the due entries themselves
/// (grouped per vehicle, same entry shape as the per-vehicle status view). Vehicles with
/// nothing due still appear with zero counts and an empty list, so the dashboard can show
/// the whole assigned fleet — and a freshly assigned unit with no completions at all shows
/// its <c>NotYetRecordedCount</c> instead of looking fully compliant. Ordered most urgent
/// first: overdue count, then due-soon count, then not-yet-recorded count, then unit number.
/// </summary>
public sealed record FleetPmDueResponse(
    IReadOnlyList<FleetVehiclePmDueResponse> Vehicles)
{
    /// <summary>
    /// The dashboard's urgency order: overdue desc, due-soon desc, not-yet-recorded desc,
    /// unit number as the stable tiebreak. Public so the ordering contract is testable.
    /// </summary>
    public static IReadOnlyList<FleetVehiclePmDueResponse> OrderByUrgency(
        IEnumerable<FleetVehiclePmDueResponse> rows) =>
        [.. rows
            .OrderByDescending(r => r.OverdueCount)
            .ThenByDescending(r => r.DueSoonCount)
            .ThenByDescending(r => r.NotYetRecordedCount)
            .ThenBy(r => r.UnitNumber, StringComparer.Ordinal)];
}

/// <summary>
/// One assigned vehicle's due picture on the fleet dashboard.
/// <c>NotYetRecordedCount</c> counts the plan lines with no completion at all — no due math
/// is possible for them, so without the count a never-serviced unit would read as compliant.
/// </summary>
public sealed record FleetVehiclePmDueResponse(
    Guid VehicleId,
    string UnitNumber,
    int CurrentOdometerKm,
    Guid PlanId,
    string PlanName,
    int DueSoonCount,
    int OverdueCount,
    int NotYetRecordedCount,
    IReadOnlyList<PmEntryStatusResponse> DueEntries);
