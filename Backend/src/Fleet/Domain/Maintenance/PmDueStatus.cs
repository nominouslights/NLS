namespace NorthernLink.Fleet.Domain.Maintenance;

/// <summary>
/// The computed due status of one code on one vehicle. Arms the code has no basis for are
/// null (no km interval or no completion → no <see cref="NextDueKm"/>, and so on).
/// <see cref="KmRemaining"/>/<see cref="DaysRemaining"/> are the distances to the arms and
/// go negative once overdue.
/// </summary>
public sealed record PmDueStatus(
    int? NextDueKm,
    DateOnly? NextDueDate,
    int? KmRemaining,
    int? DaysRemaining,
    PmDueState State);
