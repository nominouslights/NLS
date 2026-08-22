using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Periods;

/// <summary>All domain errors the BudgetPeriod aggregate (and its handlers) can produce.</summary>
public static class BudgetPeriodErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Budgeting.Period.NotFound", "The budget period was not found.");

    public static readonly Error YearOutOfRange = Error.Validation(
        "Budgeting.Period.YearOutOfRange",
        $"The year must be between {BudgetPeriod.YearMin} and {BudgetPeriod.YearMax}.");

    public static readonly Error OrdinalOutOfRange = Error.Validation(
        "Budgeting.Period.OrdinalOutOfRange",
        "The ordinal must be 1-12 for a month period or 1-4 for a quarter period.");

    public static readonly Error Overlapping = Error.Conflict(
        "Budgeting.Period.Overlapping",
        "The period overlaps an existing budget period. Budget periods never share a day.");
}
