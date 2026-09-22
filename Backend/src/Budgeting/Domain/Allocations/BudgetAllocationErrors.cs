using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Allocations;

/// <summary>All domain errors the BudgetAllocation aggregate (and its handlers) can produce.</summary>
public static class BudgetAllocationErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Budgeting.Allocation.NotFound", "There is no allocation for that budget code in this period.");

    public static readonly Error AmountRequired = Error.Validation(
        "Budgeting.Allocation.AmountRequired", "An allocation needs an amount. Zero is allowed.");

    public static readonly Error AmountNegative = Error.Validation(
        "Budgeting.Allocation.AmountNegative", "The amount cannot be negative.");

    // Spelled out rather than interpolated from AmountMax: a static initializer formats under
    // whatever culture the process started in, and "999.999.999,99" is not the message wanted.
    public static readonly Error AmountTooLarge = Error.Validation(
        "Budgeting.Allocation.AmountTooLarge",
        "The amount must be 999,999,999.99 or less.");

    public static readonly Error JustificationRequired = Error.Validation(
        "Budgeting.Allocation.JustificationRequired",
        "Every allocation needs a justification — this is zero-based budgeting, so each line is argued from zero.");

    public static readonly Error JustificationTooLong = Error.Validation(
        "Budgeting.Allocation.JustificationTooLong",
        $"The justification must be {BudgetAllocation.JustificationMaxLength} characters or fewer.");

    // --- Cross-aggregate guards the set/remove handlers apply. Both are Conflicts: the request
    // was well-formed, the world is not in a state to accept it. ---

    public static readonly Error PeriodNotEditable = Error.Conflict(
        "Budgeting.Allocation.PeriodNotEditable",
        "The plan can only change while the period is Draft or Open.");

    public static readonly Error CodeRetired = Error.Conflict(
        "Budgeting.Allocation.CodeRetired",
        "That budget code is retired and cannot take new allocations. Restore it or pick another code.");

    // --- Copy-from-an-earlier-period guards. Two period ids are in play, so the source gets its
    // own NotFound rather than reusing BudgetPeriodErrors.NotFound: the console has to be able to
    // say *which* period was wrong, and "the period was not found" for a request that names two
    // of them is the kind of message that costs somebody an afternoon. ---

    public static readonly Error CopySourceRequired = Error.Validation(
        "Budgeting.Allocation.CopySourceRequired",
        "Choose a period to copy from.");

    public static readonly Error CopySourceIsTarget = Error.Validation(
        "Budgeting.Allocation.CopySourceIsTarget",
        "A period cannot be copied onto itself. Choose a different source period.");

    public static readonly Error CopySourceNotFound = Error.NotFound(
        "Budgeting.Allocation.CopySourceNotFound",
        "The period to copy from was not found.");
}
