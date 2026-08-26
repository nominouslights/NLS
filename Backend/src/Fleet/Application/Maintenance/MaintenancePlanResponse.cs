namespace NorthernLink.Fleet.Application.Maintenance;

/// <summary>
/// The Fleet module's public representation of a full maintenance plan, including every
/// routine item and overhaul. <c>Tier</c>/<c>Task</c> travel as the enum names
/// ("Primary", "Inspect", …), matching the module's other responses.
/// </summary>
public sealed record MaintenancePlanResponse(
    Guid Id,
    string Name,
    string VehicleModel,
    string ServiceClass,
    string? Notes,
    IReadOnlyList<MaintenanceItemResponse> Items,
    IReadOnlyList<OverhaulSpecResponse> Overhauls,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>One routine maintenance line of a plan. Null leads mean the plan-wide defaults apply.</summary>
public sealed record MaintenanceItemResponse(
    string Code,
    string System,
    string Component,
    string Tier,
    string Task,
    int? IntervalKm,
    int? IntervalMonths,
    int ShopMinutes,
    int? LeadKm,
    int? LeadDays,
    string? Notes);

/// <summary>One major-component overhaul of a plan. Null leads mean the plan-wide defaults apply.</summary>
public sealed record OverhaulSpecResponse(
    string Code,
    string Component,
    int? IntervalKm,
    int? IntervalMonths,
    decimal LabourHours,
    decimal PartsCad,
    int? LeadKm,
    int? LeadDays,
    string Scope,
    IReadOnlyList<string> ConditionTriggers,
    IReadOnlyList<string> RelatedItemCodes);
