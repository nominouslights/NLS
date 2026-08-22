namespace NorthernLink.Budgeting.Domain.Codes;

/// <summary>
/// How often a budget code's continued existence is meant to be re-examined. Required on every
/// code, defaulting to <see cref="Quarterly"/> — zero-based budgeting only works if someone is
/// on the hook to revisit each code, and "never" is not an option the model offers.
/// <para>
/// Declarative today: nothing schedules a review or nags anyone. It records the intent so a
/// future review-due report has something to sort by.
/// </para>
/// </summary>
public enum BudgetReviewFrequency
{
    Monthly,
    Quarterly,
    Annual,
}
