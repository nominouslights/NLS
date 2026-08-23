namespace NorthernLink.Fleet.Domain.Maintenance;

/// <summary>
/// One routine maintenance line of a <see cref="MaintenancePlan"/> — a component with the
/// task performed on it at an interval in km and/or months (null = that axis does not
/// apply). <see cref="Code"/> ("PM-E-001", …) is the stable natural key completions
/// reference. Persisted as jsonb.
/// </summary>
public sealed record MaintenanceItem
{
    public required string Code { get; init; }
    public required string System { get; init; }
    public required string Component { get; init; }
    public ComponentTier Tier { get; init; }
    public MaintenanceTask Task { get; init; }
    public int? IntervalKm { get; init; }
    public int? IntervalMonths { get; init; }
    public int ShopMinutes { get; init; }
    public string? Notes { get; init; }
}
