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

    /// <summary>
    /// Today's UTC date — the single "today" every PM computation and guard shares
    /// (<see cref="Compute"/> callers and <see cref="PmCompletion.Log"/>'s future-date guard).
    /// </summary>
    public static DateOnly TodayUtc() => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Computes the due status of one code. <paramref name="leadKm"/>/<paramref name="leadDays"/>
    /// are the per-line due-soon lead overrides (MaintenanceItem/OverhaulSpec.LeadKm/LeadDays);
    /// null falls back to the defaults. A <paramref name="lastDone"/> with neither interval arm
    /// set returns <see cref="PmDueState.Ok"/> with no due math — by design: plan validation
    /// guarantees every item and overhaul has at least one arm, so that input cannot come from
    /// a stored plan.
    /// </summary>
    public static PmDueStatus Compute(
        int? intervalKm,
        int? intervalMonths,
        PmLastDone? lastDone,
        int currentOdometerKm,
        DateOnly today,
        int? leadKm = null,
        int? leadDays = null)
    {
        if (lastDone is null)
        {
            return new PmDueStatus(null, null, null, null, PmDueState.NotYetRecorded);
        }

        int? nextDueKm = intervalKm is > 0 ? AddKmClamped(lastDone.OdometerKm, intervalKm.Value) : null;
        DateOnly? nextDueDate = intervalMonths is > 0
            ? AddMonthsClamped(lastDone.Date, intervalMonths.Value)
            : null;

        int? kmRemaining = nextDueKm - currentOdometerKm;
        int? daysRemaining = nextDueDate is { } dueDate ? dueDate.DayNumber - today.DayNumber : null;

        var effectiveLeadKm = leadKm ?? DefaultLeadKm;
        var effectiveLeadDays = leadDays ?? DefaultLeadDays;

        var state = PmDueState.Ok;
        if (kmRemaining is <= 0 || daysRemaining is <= 0)
        {
            state = PmDueState.Overdue;
        }
        else if (kmRemaining <= effectiveLeadKm || daysRemaining <= effectiveLeadDays)
        {
            state = PmDueState.DueSoon;
        }

        return new PmDueStatus(nextDueKm, nextDueDate, kmRemaining, daysRemaining, state);
    }

    /// <summary>
    /// The km twin of <see cref="AddMonthsClamped"/>: computed in <see cref="long"/> so a
    /// huge last-done odometer plus a huge interval cannot wrap negative, clamping to
    /// <see cref="int.MaxValue"/> (permanently "not due yet" on the km arm — the honest
    /// reading of an interval that runs off the odometer).
    /// </summary>
    private static int AddKmClamped(int lastDoneKm, int intervalKm)
    {
        var nextDue = (long)lastDoneKm + intervalKm;
        return nextDue > int.MaxValue ? int.MaxValue : (int)nextDue;
    }

    /// <summary>
    /// <see cref="DateOnly.AddMonths"/> throws past <see cref="DateOnly.MaxValue"/>; a
    /// far-future completion date must not make Compute throw, so anything beyond the
    /// calendar's end clamps to <see cref="DateOnly.MaxValue"/> (permanently "not due yet" on
    /// that arm, which is the honest reading of an interval that runs off the calendar).
    /// </summary>
    private static DateOnly AddMonthsClamped(DateOnly date, int months)
    {
        // Zero-based month index since year 1; DateOnly.MaxValue is 9999-12-31, index 12*9999-1.
        var targetMonthIndex = (long)(date.Year - 1) * 12 + (date.Month - 1) + months;
        return targetMonthIndex >= 12L * DateOnly.MaxValue.Year
            ? DateOnly.MaxValue
            : date.AddMonths(months);
    }
}
