using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Actuals;

/// <summary>
/// All domain errors the ActualTransaction aggregate (and its handlers) can produce. Every
/// message here is user-facing copy, not a log line: the Budgeting console shows the server's
/// text verbatim, so each one names the rule in plain words and, where there is one, the way out.
/// </summary>
public static class ActualTransactionErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Budgeting.Actual.NotFound", "There is no such transaction.");

    public static readonly Error AmountRequired = Error.Validation(
        "Budgeting.Actual.AmountRequired", "A transaction needs an amount.");

    // Expenses are positive amounts on an Expense code — the code's category says which side of
    // the ledger a figure lands on, so the minus sign has no second job to do here.
    public static readonly Error AmountNegative = Error.Validation(
        "Budgeting.Actual.AmountNegative",
        "The amount cannot be negative. Record an expense as a positive amount on an expense code.");

    // Spelled out rather than interpolated from AmountMax: a static initializer formats under
    // whatever culture the process started in, and "999.999.999,99" is not the message wanted.
    public static readonly Error AmountTooLarge = Error.Validation(
        "Budgeting.Actual.AmountTooLarge",
        "The amount must be 999,999,999.99 or less.");

    public static readonly Error OccurredOnRequired = Error.Validation(
        "Budgeting.Actual.OccurredOnRequired",
        "A transaction needs the date it happened — that date is what decides which budget period it counts in.");

    public static readonly Error DescriptionRequired = Error.Validation(
        "Budgeting.Actual.DescriptionRequired", "A transaction needs a description of what it was for.");

    public static readonly Error DescriptionTooLong = Error.Validation(
        "Budgeting.Actual.DescriptionTooLong",
        $"The description must be {ActualTransaction.DescriptionMaxLength} characters or fewer.");

    public static readonly Error ExternalReferenceTooLong = Error.Validation(
        "Budgeting.Actual.ExternalReferenceTooLong",
        $"The external reference must be {ActualTransaction.ExternalReferenceMaxLength} characters or fewer.");

    // --- Cross-aggregate guards the handlers apply. Both are Conflicts: the request was
    // well-formed, the world is not in a state to accept it. ---

    // Deliberately not a "period is read-only" guard. Actuals are facts and are accepted against
    // a period in any state, Closed included — only the plan is frozen by the lifecycle. See the
    // asymmetry paragraph on ActualTransaction.
    public static readonly Error NoPeriodCoversDate = Error.Conflict(
        "Budgeting.Actual.NoPeriodCoversDate",
        "No budget period covers that date, so this transaction would never show up anywhere. Create the period first, or check the date.");

    public static readonly Error CodeRetired = Error.Conflict(
        "Budgeting.Actual.CodeRetired",
        "That budget code is retired and cannot take new transactions. Restore it or pick another code.");
}
