using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Codes;

/// <summary>All domain errors the BudgetCode aggregate (and its handlers) can produce.</summary>
public static class BudgetCodeErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Budgeting.Code.NotFound", "The budget code was not found in this period.");

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
        "Another budget code in this period already uses that code. Codes are unique within a period.");

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

    // --- The cost centre must name an entry of the tenant's register (BudgetCodeCostCentreRule).
    // Checked in the application layer: the aggregate cannot see the register. ---

    public static readonly Error CostCentreNotFound = Error.Validation(
        "Budgeting.Code.CostCentreNotFound",
        "That cost centre is not in the cost-centre register. Add it to the register first, or choose an existing one.");

    /// <summary>
    /// Only when the value is being set or changed: a code that already carries a cost centre
    /// which has since been retired keeps it, and can still be saved with it unchanged.
    /// </summary>
    public static readonly Error CostCentreRetired = Error.Conflict(
        "Budgeting.Code.CostCentreRetired",
        "That cost centre is retired and cannot be given to a budget code. Choose an active one, or restore it in the register first.");

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
        "Budgeting.Code.ParentNotFound",
        "The parent budget code was not found in this period. A code can only roll up into a code of the same period.");

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

    // --- Retirement refused because the code's own period uses it (BudgetCodeRetirementRule).
    // Both share the code Budgeting.Code.PeriodNotClosed, kept from when the remedy was "close the
    // period" so the console's handling (it shows the message verbatim) needs no change; the two
    // messages name the remedy that actually works under the current rules. Built at call time
    // because the period label is not known until the rule has run. ---

    /// <summary>
    /// Retirement refused because budget items of the code's period are planned on it. The way out
    /// is to remove those items — the plan is editable in Draft and Open, the only states in which
    /// a code can be retired at all.
    /// </summary>
    public static Error RetirementBlockedByItems(string periodLabel) => Error.Conflict(
        "Budgeting.Code.PeriodNotClosed",
        $"{periodLabel} has budget items on this code. Remove its items first, then retire the code — a planned line has to keep resolving to a live code.");

    /// <summary>
    /// Retirement refused because spend dated inside the code's period is recorded against it.
    /// Recorded spend is a fact, not a plan line, so the message offers no remedy: the period never
    /// becomes retirable later because its chart freezes outside Draft and Open, and correcting a
    /// miscoded transaction (re-pointing or deleting it) is a ledger fix, not a retirement path
    /// worth advertising. Said plainly rather than pointing at a remedy that fails.
    /// </summary>
    public static Error RetirementBlockedBySpend(string periodLabel) => Error.Conflict(
        "Budgeting.Code.PeriodNotClosed",
        $"This budget code has spend recorded in {periodLabel}. A code with spend recorded in its period cannot be retired — its transactions have to keep resolving to a live code.");

    // --- The chart follows the period lifecycle. A Conflict, like the allocation guard it
    // mirrors: the request was well-formed, the period is not in a state to accept it. ---

    public static readonly Error PeriodNotEditable = Error.Conflict(
        "Budgeting.Code.PeriodNotEditable",
        "A period's budget codes can only change while it is Draft or Open.");

    // --- Copy-codes-from-another-period guards. Two period ids are in play, so the source gets
    // its own NotFound (the BudgetAllocationErrors.CopySourceNotFound reasoning). ---

    public static readonly Error CopySourceRequired = Error.Validation(
        "Budgeting.Code.CopySourceRequired",
        "Choose a period to copy budget codes from.");

    public static readonly Error CopySourceIsTarget = Error.Validation(
        "Budgeting.Code.CopySourceIsTarget",
        "A period's budget codes cannot be copied onto itself. Choose a different source period.");

    public static readonly Error CopySourceNotFound = Error.NotFound(
        "Budgeting.Code.CopySourceNotFound",
        "The period to copy budget codes from was not found.");
}
