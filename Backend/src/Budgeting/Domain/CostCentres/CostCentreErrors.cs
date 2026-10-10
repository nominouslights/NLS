using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.CostCentres;

/// <summary>All domain errors the CostCentre aggregate (and its handlers) can produce.</summary>
public static class CostCentreErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Budgeting.CostCentre.NotFound", "The cost centre was not found.");

    public static readonly Error CodeRequired = Error.Validation(
        "Budgeting.CostCentre.CodeRequired", "A cost centre needs a code.");

    public static readonly Error CodeTooLong = Error.Validation(
        "Budgeting.CostCentre.CodeTooLong",
        $"The cost-centre code must be {CostCentre.CodeMaxLength} characters or fewer.");

    /// <summary>
    /// A PUT that names a different code. The code is the string budget codes carry, so it is
    /// decided once; the message names the alternative.
    /// </summary>
    public static readonly Error CodeImmutable = Error.Validation(
        "Budgeting.CostCentre.CodeImmutable",
        "A cost centre's code cannot be changed — budget codes carry it by that code. Retire this cost centre and create a new one instead.");

    public static readonly Error DuplicateCode = Error.Conflict(
        "Budgeting.CostCentre.DuplicateCode",
        "Another cost centre already uses that code.");

    public static readonly Error NameRequired = Error.Validation(
        "Budgeting.CostCentre.NameRequired", "A cost centre needs a name.");

    public static readonly Error NameTooLong = Error.Validation(
        "Budgeting.CostCentre.NameTooLong",
        $"The name must be {CostCentre.NameMaxLength} characters or fewer.");

    public static readonly Error DescriptionTooLong = Error.Validation(
        "Budgeting.CostCentre.DescriptionTooLong",
        $"The description must be {CostCentre.DescriptionMaxLength} characters or fewer.");

    public static readonly Error OwnerNotFound = Error.NotFound(
        "Budgeting.CostCentre.OwnerNotFound", "The owner is not a user of this tenant.");

    // --- Hierarchy: one level deep, guarded from above and below (the BudgetCodeParentRule shape). ---

    public static readonly Error ParentNotFound = Error.NotFound(
        "Budgeting.CostCentre.ParentNotFound", "The parent cost centre was not found.");

    public static readonly Error ParentIsSelf = Error.Validation(
        "Budgeting.CostCentre.ParentIsSelf", "A cost centre cannot be its own parent.");

    public static readonly Error ParentIsNotTopLevel = Error.Validation(
        "Budgeting.CostCentre.ParentIsNotTopLevel",
        "That cost centre already rolls up into another. The hierarchy is one level deep, so only a top-level cost centre can be a parent.");

    public static readonly Error ParentRetired = Error.Conflict(
        "Budgeting.CostCentre.ParentRetired",
        "That parent cost centre is retired. Choose an active one, or restore it first.");

    public static readonly Error HasChildrenCannotHaveParent = Error.Conflict(
        "Budgeting.CostCentre.HasChildrenCannotHaveParent",
        "Other cost centres roll up into this one, so it cannot roll up into another. The hierarchy is one level deep.");

    public static readonly Error HasActiveChildren = Error.Conflict(
        "Budgeting.CostCentre.HasActiveChildren",
        "Active cost centres still roll up into this one. Retire them, or move them to another parent, first.");

    public static readonly Error HasChildren = Error.Conflict(
        "Budgeting.CostCentre.HasChildren",
        "Other cost centres roll up into this one and would be left pointing at nothing. Move or delete them first, or retire this cost centre instead.");

    public static readonly Error InUse = Error.Conflict(
        "Budgeting.CostCentre.InUse",
        "Budget codes carry this cost centre, so it cannot be deleted. Retire it instead — a retired cost centre stays listed so existing codes keep resolving.");
}
