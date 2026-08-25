namespace NorthernLink.Fleet.Application.Maintenance;

/// <summary>
/// The full computed PM schedule of one vehicle — every plan item and overhaul with its due
/// status. <see cref="Assigned"/> false means the vehicle follows no plan yet: the other
/// fields are null/empty and the frontend renders the assign call-to-action
/// (<c>200 {{ assigned: false }}</c>, never a 404).
/// </summary>
public sealed record VehiclePmStatusResponse(
    bool Assigned,
    Guid? PlanId,
    string? PlanName,
    DateTimeOffset? AssignedAtUtc,
    int? CurrentOdometerKm,
    IReadOnlyList<PmEntryStatusResponse> Entries);

/// <summary>
/// One plan line (item or overhaul) with its computed due status on one vehicle.
/// <c>Kind</c> is "Item" or "Overhaul"; overhaul entries carry <c>System</c> "Overhauls",
/// no tier/task, and their labour hours converted to <c>ShopMinutes</c>. <c>State</c> is the
/// PmDueState name (NotYetRecorded / Ok / DueSoon / Overdue). Last-done and due fields are
/// null when never recorded or when that interval arm does not apply.
/// </summary>
public sealed record PmEntryStatusResponse(
    string Code,
    string Kind,
    string System,
    string Component,
    string? Tier,
    string? Task,
    int? IntervalKm,
    int? IntervalMonths,
    int ShopMinutes,
    int? LastDoneKm,
    DateOnly? LastDoneDate,
    int? NextDueKm,
    DateOnly? NextDueDate,
    int? KmRemaining,
    int? DaysRemaining,
    string State);
