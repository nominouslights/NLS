namespace NorthernLink.Fleet.Domain.Maintenance;

/// <summary>
/// A long-interval major-component overhaul of a <see cref="MaintenancePlan"/> — its scope,
/// labour/parts estimate, the condition findings that justify overhauling early, and the
/// Test items (<see cref="RelatedItemCodes"/>) whose latest measurements inform that
/// decision. <see cref="Code"/> ("OH-01", …) is the stable natural key. Persisted as jsonb.
/// </summary>
public sealed record OverhaulSpec
{
    public required string Code { get; init; }
    public required string Component { get; init; }
    public int? IntervalKm { get; init; }
    public int? IntervalMonths { get; init; }
    public decimal LabourHours { get; init; }
    public decimal PartsCad { get; init; }
    public required string Scope { get; init; }
    public List<string> ConditionTriggers { get; init; } = [];
    public List<string> RelatedItemCodes { get; init; } = [];
}
