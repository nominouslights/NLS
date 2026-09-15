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

    // --- Lifecycle. One Conflict per transition, each naming the state it needed. The
    // lifecycle is forward-only, so "wrong source state" is the only way a transition fails. ---

    public static readonly Error NotDraft = Error.Conflict(
        "Budgeting.Period.NotDraft", "Only a draft plan can be finalized.");

    public static readonly Error NotFinalized = Error.Conflict(
        "Budgeting.Period.NotFinalized", "Only a finalized plan can be opened.");

    public static readonly Error NotOpen = Error.Conflict(
        "Budgeting.Period.NotOpen", "Only an open period can go into review.");

    public static readonly Error NotInReview = Error.Conflict(
        "Budgeting.Period.NotInReview", "Only a period in review can be closed.");
}
