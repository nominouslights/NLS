namespace NorthernLink.Fleet.Application.Maintenance;

/// <summary>
/// The fleet-wide PM dashboard package: one row per assigned, non-disposed vehicle, each
/// carrying its DueSoon/Overdue counts and the due entries themselves (grouped per vehicle,
/// same entry shape as the per-vehicle status view). Vehicles with nothing due still appear
/// with zero counts and an empty list, so the dashboard can show the whole assigned fleet.
/// Ordered most urgent first: overdue count, then due-soon count, then unit number.
/// </summary>
public sealed record FleetPmDueResponse(
    IReadOnlyList<FleetVehiclePmDueResponse> Vehicles);

/// <summary>One assigned vehicle's due picture on the fleet dashboard.</summary>
public sealed record FleetVehiclePmDueResponse(
    Guid VehicleId,
    string UnitNumber,
    int CurrentOdometerKm,
    Guid PlanId,
    string PlanName,
    int DueSoonCount,
    int OverdueCount,
    IReadOnlyList<PmEntryStatusResponse> DueEntries);
