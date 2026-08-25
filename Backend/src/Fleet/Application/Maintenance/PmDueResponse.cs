namespace NorthernLink.Fleet.Application.Maintenance;

/// <summary>
/// The shop-visit package for one vehicle: everything DueSoon or Overdue grouped by system,
/// with <see cref="TotalShopMinutes"/> summing the due entries' shop minutes (overhauls
/// contribute their labour hours in minutes), plus the codes never logged at all
/// (<see cref="NotYetRecorded"/> — no due math is possible for them, flagged separately so
/// they are not silently invisible). <see cref="Assigned"/> false = no plan, empty package.
/// </summary>
public sealed record PmDueResponse(
    bool Assigned,
    Guid? PlanId,
    string? PlanName,
    int? CurrentOdometerKm,
    int TotalShopMinutes,
    IReadOnlyList<PmDueGroupResponse> Groups,
    IReadOnlyList<PmEntryStatusResponse> NotYetRecorded);

/// <summary>The due entries of one system ("Engine", "Brakes", … or "Overhauls").</summary>
public sealed record PmDueGroupResponse(
    string System,
    IReadOnlyList<PmEntryStatusResponse> Entries);
