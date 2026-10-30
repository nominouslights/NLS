namespace NorthernLink.Budgeting.Domain.Allocations;

/// <summary>
/// Whether a budget item is a one-off or something that will be owed again next period.
/// Declarative: nothing schedules or repeats an item — "start from the previous period" (the copy)
/// is still how a recurring item reaches the next plan. Stored and sent as its name.
/// </summary>
public enum BudgetRecurrence
{
    OneTime,
    Recurring,
}
