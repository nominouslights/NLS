namespace NorthernLink.Fleet.Domain.Maintenance;

/// <summary>
/// Computed due state of one maintenance item or overhaul on one vehicle. Never stored —
/// always derived at query time by <see cref="PmSchedule.Compute"/> from the latest
/// completion, the vehicle's current odometer, and today's date.
/// </summary>
public enum PmDueState
{
    /// <summary>No completion has ever been logged for the code — no due math possible.</summary>
    NotYetRecorded,
    Ok,
    DueSoon,
    Overdue,
}
