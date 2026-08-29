namespace NorthernLink.Fleet.Domain.Maintenance;

/// <summary>
/// A long-interval major-component overhaul of a <see cref="MaintenancePlan"/> — its scope,
/// labour/parts estimate, the condition findings that justify overhauling early, and the
/// Test items (<see cref="RelatedItemCodes"/>) whose latest measurements inform that
/// decision. <see cref="Code"/> ("OH-01", …) is the stable natural key.
/// <see cref="LeadKm"/>/<see cref="LeadDays"/> optionally override the
/// <see cref="PmSchedule"/> due-soon leads for this overhaul (null = the plan-wide default) —
/// a 320,000 km overhaul warrants far more warning than a routine service line.
/// Persisted as jsonb.
/// </summary>
public sealed record OverhaulSpec
{
    public required string Code { get; init; }
    public required string Component { get; init; }
    public int? IntervalKm { get; init; }
    public int? IntervalMonths { get; init; }
    public decimal LabourHours { get; init; }
    public decimal PartsCad { get; init; }
    public int? LeadKm { get; init; }
    public int? LeadDays { get; init; }
    public required string Scope { get; init; }
    public List<string> ConditionTriggers { get; init; } = [];
    public List<string> RelatedItemCodes { get; init; } = [];

    /// <summary>
    /// The overhaul's contribution to shop time — its labour hours in minutes, rounded away
    /// from zero. Computed (get-only), so EF's jsonb mapping ignores it and it can never
    /// drift from <see cref="LabourHours"/>.
    /// </summary>
    public int ShopMinutes => (int)Math.Round(LabourHours * 60m, MidpointRounding.AwayFromZero);
}
