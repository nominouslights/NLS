using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Allocations;

/// <summary>All domain errors the BudgetAllocation aggregate (and its handlers) can produce.</summary>
public static class BudgetAllocationErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Budgeting.Allocation.NotFound", "That budget item was not found in this period.");

    public static readonly Error AmountRequired = Error.Validation(
        "Budgeting.Allocation.AmountRequired",
        "Enter an amount, or a quantity and a unit cost. Zero is allowed.");

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

    // --- Budget item details. The console shows every message verbatim. The two money ceilings
    // are spelled out for the same culture reason as AmountTooLarge. ---

    // Checked by the create/update handlers (the aggregate never sees a missing id): a nullable
    // id on the wire, so an omitted code reads as this rather than a 404 for Guid.Empty.
    public static readonly Error CodeRequired = Error.Validation(
        "Budgeting.Allocation.CodeRequired",
        "Choose the budget code this item is planned against.");

    public static readonly Error TitleRequired = Error.Validation(
        "Budgeting.Allocation.TitleRequired",
        "Give the budget item a title — what is this money for?");

    public static readonly Error TitleTooLong = Error.Validation(
        "Budgeting.Allocation.TitleTooLong",
        $"The title must be {BudgetAllocation.TitleMaxLength} characters or fewer.");

    public static readonly Error QuantityWithoutUnitCost = Error.Validation(
        "Budgeting.Allocation.QuantityWithoutUnitCost",
        "Quantity and unit cost go together: enter both, or leave both blank and enter a lump-sum amount.");

    public static readonly Error QuantityNotPositive = Error.Validation(
        "Budgeting.Allocation.QuantityNotPositive",
        "The quantity must be greater than zero.");

    public static readonly Error QuantityTooLarge = Error.Validation(
        "Budgeting.Allocation.QuantityTooLarge",
        "The quantity must be 999,999,999.99 or less.");

    public static readonly Error UnitCostNegative = Error.Validation(
        "Budgeting.Allocation.UnitCostNegative",
        "The unit cost cannot be negative.");

    public static readonly Error UnitCostTooLarge = Error.Validation(
        "Budgeting.Allocation.UnitCostTooLarge",
        "The unit cost must be 999,999,999.99 or less.");

    public static readonly Error UnitTooLong = Error.Validation(
        "Budgeting.Allocation.UnitTooLong",
        $"The unit must be {BudgetAllocation.UnitMaxLength} characters or fewer.");

    public static readonly Error SpendTypeInvalid = Error.Validation(
        "Budgeting.Allocation.SpendTypeInvalid",
        "The spend type must be Operating or Capital.");

    public static readonly Error RecurrenceInvalid = Error.Validation(
        "Budgeting.Allocation.RecurrenceInvalid",
        "The recurrence must be OneTime or Recurring.");

    public static readonly Error PriorityInvalid = Error.Validation(
        "Budgeting.Allocation.PriorityInvalid",
        "The priority must be MustHave, ShouldHave or NiceToHave.");

    public static readonly Error VendorTooLong = Error.Validation(
        "Budgeting.Allocation.VendorTooLong",
        $"The vendor must be {BudgetAllocation.VendorMaxLength} characters or fewer.");

    public static readonly Error TooManyTags = Error.Validation(
        "Budgeting.Allocation.TooManyTags",
        $"A budget item can carry at most {BudgetAllocation.MaxTags} tags.");

    public static readonly Error TagInvalid = Error.Validation(
        "Budgeting.Allocation.TagInvalid",
        $"Each tag must be 1 to {BudgetAllocation.TagMaxLength} characters.");

    public static readonly Error AssumptionsTooLong = Error.Validation(
        "Budgeting.Allocation.AssumptionsTooLong",
        $"The assumptions must be {BudgetAllocation.AssumptionsMaxLength} characters or fewer.");

    public static readonly Error ConsequenceTooLong = Error.Validation(
        "Budgeting.Allocation.ConsequenceTooLong",
        $"The consequence if unfunded must be {BudgetAllocation.ConsequenceMaxLength} characters or fewer.");

    // --- Cross-aggregate guards the create/update/remove handlers apply. Both are Conflicts: the request
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
