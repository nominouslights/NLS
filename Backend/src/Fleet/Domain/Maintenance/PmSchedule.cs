namespace NorthernLink.Fleet.Domain.Maintenance;

/// <summary>
/// The one due calculator — used identically for maintenance items and overhauls, by read
/// services and tests alike. Due status is always computed from the latest completion, the
/// vehicle's current odometer, and today's date; it is never stored, so plan edits are
/// retroactive by construction. Whichever arm (km or calendar) hits first governs.
/// </summary>
public static class PmSchedule
{
    /// <summary>Km before the km arm at which a code turns <see cref="PmDueState.DueSoon"/>.</summary>
    public const int DefaultLeadKm = 2000;

    /// <summary>Days before the calendar arm at which a code turns <see cref="PmDueState.DueSoon"/>.</summary>
    public const int DefaultLeadDays = 30;

    public static PmDueStatus Compute(
        int? intervalKm,
        int? intervalMonths,
        PmLastDone? lastDone,
        int currentOdometerKm,
        DateOnly today)
    {
        if (lastDone is null)
        {
            return new PmDueStatus(null, null, null, null, PmDueState.NotYetRecorded);
        }

        int? nextDueKm = intervalKm is > 0 ? lastDone.OdometerKm + intervalKm.Value : null;
        DateOnly? nextDueDate = intervalMonths is > 0 ? lastDone.Date.AddMonths(intervalMonths.Value) : null;

        int? kmRemaining = nextDueKm - currentOdometerKm;
        int? daysRemaining = nextDueDate is { } dueDate ? dueDate.DayNumber - today.DayNumber : null;

        var state = PmDueState.Ok;
        if (kmRemaining is <= 0 || daysRemaining is <= 0)
        {
            state = PmDueState.Overdue;
        }
        else if (kmRemaining is <= DefaultLeadKm || daysRemaining is <= DefaultLeadDays)
        {
            state = PmDueState.DueSoon;
        }

        return new PmDueStatus(nextDueKm, nextDueDate, kmRemaining, daysRemaining, state);
    }
}
