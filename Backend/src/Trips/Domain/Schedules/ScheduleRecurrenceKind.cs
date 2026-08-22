namespace NorthernLink.Trips.Domain.Schedules;

/// <summary>
/// How a schedule template's occurrences are spaced across the generation horizon. Each
/// kind reads only its own fields: <see cref="DaysOfWeek"/> uses the weekday list,
/// <see cref="EveryNDays"/> an interval anchored on a date, <see cref="MonthlyDays"/> a
/// set of calendar days (clamped to month-end).
/// </summary>
public enum ScheduleRecurrenceKind
{
    DaysOfWeek,
    EveryNDays,
    MonthlyDays,
}
