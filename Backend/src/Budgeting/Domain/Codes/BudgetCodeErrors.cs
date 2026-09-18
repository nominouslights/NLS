using System.Globalization;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Codes;

/// <summary>All domain errors the BudgetCode aggregate (and its handlers) can produce.</summary>
public static class BudgetCodeErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Budgeting.Code.NotFound", "The budget code was not found.");

    public static readonly Error CodeRequired = Error.Validation(
        "Budgeting.Code.CodeRequired", "A budget code needs a code.");

    public static readonly Error CodeTooLong = Error.Validation(
        "Budgeting.Code.CodeTooLong",
        $"The code must be {BudgetCode.CodeMaxLength} characters or fewer.");

    public static readonly Error CodeInvalidFormat = Error.Validation(
        "Budgeting.Code.CodeInvalidFormat",
        "The code may use letters, digits and hyphens only, and must start and end with a letter or digit (for example FLEET-MAINT).");

    public static readonly Error DuplicateCode = Error.Conflict(
        "Budgeting.Code.DuplicateCode",
        "Another budget code already uses that code. Codes are unique within a tenant.");

    public static readonly Error NameRequired = Error.Validation(
        "Budgeting.Code.NameRequired", "A budget code needs a name.");

    public static readonly Error NameTooLong = Error.Validation(
        "Budgeting.Code.NameTooLong",
        $"The name must be {BudgetCode.NameMaxLength} characters or fewer.");

    public static readonly Error DescriptionTooLong = Error.Validation(
        "Budgeting.Code.DescriptionTooLong",
        $"The description must be {BudgetCode.DescriptionMaxLength} characters or fewer.");

    public static readonly Error CostCentreTooLong = Error.Validation(
        "Budgeting.Code.CostCentreTooLong",
        $"The cost centre must be {BudgetCode.CostCentreMaxLength} characters or fewer.");

    public static readonly Error CostCentreNotAllowedForRevenue = Error.Validation(
        "Budgeting.Code.CostCentreNotAllowedForRevenue",
        "A revenue code cannot carry a cost centre — a cost centre attributes cost, not revenue.");

    public static readonly Error GlAccountCodeTooLong = Error.Validation(
        "Budgeting.Code.GlAccountCodeTooLong",
        $"The GL account code must be {BudgetCode.GlAccountCodeMaxLength} characters or fewer.");

    // Enum range. Reachable only through a numeric payload — a bad enum *name* is rejected by the
    // JSON converter before a command is ever built.
    public static readonly Error CategoryInvalid = Error.Validation(
        "Budgeting.Code.CategoryInvalid", "The category must be Revenue or Expense.");

    public static readonly Error ReviewFrequencyInvalid = Error.Validation(
        "Budgeting.Code.ReviewFrequencyInvalid", "The review frequency must be Monthly, Quarterly or Annual.");

    public static readonly Error ServiceLineInvalid = Error.Validation(
        "Budgeting.Code.ServiceLineInvalid", "That is not a recognized service line.");

    public static readonly Error TaxTreatmentInvalid = Error.Validation(
        "Budgeting.Code.TaxTreatmentInvalid",
        "The tax treatment must be GstApplicable, ZeroRated, Exempt or NotApplicable.");

    // --- Hierarchy. The rollup is one level deep, guarded from above and below. ---

    public static readonly Error ParentNotFound = Error.NotFound(
        "Budgeting.Code.ParentNotFound", "The parent budget code was not found.");

    public static readonly Error ParentIsSelf = Error.Validation(
        "Budgeting.Code.ParentIsSelf", "A budget code cannot be its own parent.");

    public static readonly Error ParentIsNotTopLevel = Error.Validation(
        "Budgeting.Code.ParentIsNotTopLevel",
        "That code already rolls up into another code. The hierarchy is one level deep, so only a top-level code can be a parent.");

    public static readonly Error CodeWithChildrenCannotHaveParent = Error.Conflict(
        "Budgeting.Code.CodeWithChildrenCannotHaveParent",
        "This code already has codes rolling up into it, so it cannot roll up into another. The hierarchy is one level deep.");

    public static readonly Error ParentHasChildren = Error.Conflict(
        "Budgeting.Code.ParentHasChildren",
        "Other budget codes roll up into this one and would be left pointing at nothing. Move or delete them first, or retire this code instead.");

    public static readonly Error BudgetOwnerNotFound = Error.NotFound(
        "Budgeting.Code.BudgetOwnerNotFound", "The budget owner is not a user of this tenant.");

    /// <summary>
    /// The platform's first "cannot delete, something references it" error. Its message names the
    /// alternative on purpose — refusing a delete without saying what to do instead is how a user
    /// ends up creating a duplicate code to work around it.
    /// </summary>
    public static readonly Error InUse = Error.Conflict(
        "Budgeting.Code.InUse",
        "This budget code is referenced by budget allocations or actual transactions and cannot be deleted. Retire it instead — a retired code stays listed so existing rows keep resolving.");

    /// <summary>
    /// Retirement refused because a period still using the code has not closed. The blocking
    /// periods are named in the message rather than merely counted, because "a period is still
    /// open" leaves the user hunting and "FY2026 Q3 is still open" is something they can act on
    /// immediately. Built at call time rather than as a static field for exactly that reason —
    /// the labels are not known until the rule has run.
    /// </summary>
    /// <param name="periodLabels">
    /// The blocking periods' display labels, earliest first. The list is capped in the message
    /// (see <see cref="MaxNamedPeriods"/>): a code that a dozen periods use needs the rule and a
    /// couple of examples, not a paragraph of period names.
    /// </param>
    public static Error PeriodNotClosed(IReadOnlyList<string> periodLabels) => Error.Conflict(
        "Budgeting.Code.PeriodNotClosed",
        $"This budget code is still used by {DescribePeriods(periodLabels)}. A budget code can only be retired once every period that uses it is closed — until then its plan and its transactions have to keep resolving to a live code.");

    /// <summary>How many blocking periods the message names before it summarizes the rest.</summary>
    public const int MaxNamedPeriods = 3;

    // Invariant culture on the count for the same reason the length messages above are spelled
    // out rather than interpolated: the process's culture is not the user's, and a thousands
    // separator chosen by the server is not something the console can undo.
    private static string DescribePeriods(IReadOnlyList<string> periodLabels) => periodLabels.Count switch
    {
        0 => "a period that is not closed yet",
        1 => $"{periodLabels[0]}, which is not closed yet",
        <= MaxNamedPeriods =>
            $"{string.Join(", ", periodLabels.Take(periodLabels.Count - 1))} and {periodLabels[^1]}, which are not closed yet",
        _ =>
            $"{string.Join(", ", periodLabels.Take(MaxNamedPeriods))} and " +
            $"{(periodLabels.Count - MaxNamedPeriods).ToString(CultureInfo.InvariantCulture)} more periods that are not closed yet",
    };
}
