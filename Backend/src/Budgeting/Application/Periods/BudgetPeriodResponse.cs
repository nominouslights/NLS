namespace NorthernLink.Budgeting.Application.Periods;

/// <summary>
/// The Budgeting module's public representation of a budget period — the shape the Budgeting
/// console's period list and period dashboard consume. <paramref name="Granularity"/> and
/// <paramref name="State"/> are the enum names as strings (Month/Quarter;
/// Draft/Finalized/Open/InReview/Closed, in lifecycle order). Dates are inclusive.
/// <para>
/// <paramref name="PlannedRevenueCad"/> and <paramref name="PlannedExpenseCad"/> are the sums of
/// the period's allocation lines on Revenue and Expense codes respectively, resolved against each
/// code's <em>current</em> category at read time (re-classifying a code moves its lines between
/// the two totals). Zero for a period with no lines. Counts and coverage are not on the wire — the
/// dashboard derives them from the lines and the code list it loads anyway.
/// </para>
/// </summary>
public sealed record BudgetPeriodResponse(
    Guid Id,
    string Label,
    string Granularity,
    int Year,
    int Ordinal,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string State,
    decimal PlannedRevenueCad,
    decimal PlannedExpenseCad,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
