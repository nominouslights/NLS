namespace NorthernLink.Fleet.Application.Maintenance;

/// <summary>
/// One row of the plan list — the plan's identity plus line counts and how many vehicles
/// currently follow it. The full item/overhaul detail comes from the by-id query.
/// </summary>
public sealed record MaintenancePlanSummaryResponse(
    Guid Id,
    string Name,
    string VehicleModel,
    string ServiceClass,
    string? Notes,
    int ItemCount,
    int OverhaulCount,
    int AssignedVehicleCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
