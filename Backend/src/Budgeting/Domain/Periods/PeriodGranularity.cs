namespace NorthernLink.Budgeting.Domain.Periods;

/// <summary>
/// How much calendar a budget period spans. Exactly one calendar month or one calendar
/// quarter — dates and label are derived from (granularity, year, ordinal), never typed.
/// </summary>
public enum PeriodGranularity
{
    Month,
    Quarter,
}
