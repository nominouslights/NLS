namespace NorthernLink.Trips.Domain.Schedules;

/// <summary>
/// What a schedule exception does to its template on one calendar date. Persisted as a
/// 32-char string (the platform's enum-as-string convention).
/// </summary>
public enum ScheduleExceptionKind
{
    /// <summary>The occurrence does not run that date — both legs of a paired template, including a next-day return.</summary>
    Skip,

    /// <summary>An additional occurrence on a date the recurrence would not fire, at the exception's own times (same-day only).</summary>
    ExtraRun,

    /// <summary>The occurrence runs, but at different times — each unset field falls back to the template's own time.</summary>
    TimeOverride,
}
