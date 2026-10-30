namespace NorthernLink.Budgeting.Domain.Allocations;

/// <summary>
/// The zero-based ranking of a budget item — what a planner cuts first when the plan does not
/// fit. <b>Declaration order is the ranking</b> (MustHave first); the allocations read sorts on
/// the enum's numeric value in memory, never on the stored string, because the names do not sort
/// alphabetically in rank order. Stored and sent as its name.
/// </summary>
public enum BudgetItemPriority
{
    MustHave,
    ShouldHave,
    NiceToHave,
}
