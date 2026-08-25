namespace NorthernLink.Fleet.Application.Maintenance;

/// <summary>
/// The overhaul-early decision view for one vehicle: every overhaul with its computed due
/// status, condition triggers, and the latest measurement from each related Test item.
/// <see cref="Assigned"/> false = no plan assigned, empty list.
/// </summary>
public sealed record PmOverhaulsResponse(
    bool Assigned,
    Guid? PlanId,
    string? PlanName,
    int? CurrentOdometerKm,
    IReadOnlyList<OverhaulStatusResponse> Overhauls);

/// <summary>
/// One overhaul's computed status. <c>State</c> is the PmDueState name; the interval/spec
/// fields come from the plan, the last-done/next-due fields from the latest completion.
/// </summary>
public sealed record OverhaulStatusResponse(
    string Code,
    string Component,
    int? IntervalKm,
    int? IntervalMonths,
    decimal LabourHours,
    decimal PartsCad,
    string Scope,
    IReadOnlyList<string> ConditionTriggers,
    int? LastDoneKm,
    DateOnly? LastDoneDate,
    int? NextDueKm,
    DateOnly? NextDueDate,
    int? KmRemaining,
    int? DaysRemaining,
    string State,
    IReadOnlyList<RelatedMeasurementResponse> RelatedMeasurements);

/// <summary>
/// The latest completion of one related Test item (the condition evidence beside an
/// overhaul). Measurement/date/km are null when the item was never logged.
/// </summary>
public sealed record RelatedMeasurementResponse(
    string ItemCode,
    string Component,
    string? Measurement,
    DateOnly? PerformedAt,
    int? OdometerKm);
