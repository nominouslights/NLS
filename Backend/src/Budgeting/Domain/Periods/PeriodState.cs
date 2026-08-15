namespace NorthernLink.Budgeting.Domain.Periods;

/// <summary>
/// Lifecycle of a budget period. Periods are always created <see cref="Draft"/>; the
/// Open and Lock transitions arrive with a later story.
/// </summary>
public enum PeriodState
{
    Draft,
    Open,
    Locked,
}
