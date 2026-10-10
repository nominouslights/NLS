using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Application.CostCentres;

/// <summary>
/// The register's one-level hierarchy rule, shared by create and edit — the
/// <c>BudgetCodeParentRule</c> shape, guarded from both directions (a parent must be top-level;
/// a cost centre that already has children cannot take a parent).
/// <para>
/// <b>The parent must be active when it is chosen</b> — on create, and on an edit that changes
/// the parent. An edit that keeps an existing parent which has since been retired is allowed.
/// That case only arises for a <em>retired</em> child. A parent cannot be retired while it has
/// active children, and a child cannot be restored under a retired parent (both in
/// <c>SetCostCentreActiveCommandHandler</c>), so an active child never has a retired parent.
/// Without the leniency, retiring a branch would freeze every retired child's name and owner.
/// </para>
/// </summary>
public static class CostCentreParentRule
{
    /// <param name="currentParentId">The entry's parent before this write; null on create.</param>
    /// <param name="checkChildren">True on update only — a new entry cannot have children.</param>
    public static async Task<Result> ValidateAsync(
        ICostCentreRepository repository,
        Guid? parentId,
        Guid selfId,
        Guid? currentParentId,
        bool checkChildren,
        CancellationToken cancellationToken)
    {
        if (parentId is not { } requestedParentId)
        {
            return Result.Success();
        }

        if (requestedParentId == selfId)
        {
            return Result.Failure(CostCentreErrors.ParentIsSelf);
        }

        // Tenant-filtered (query filter + RLS), so another tenant's id reads back null and
        // reports ParentNotFound rather than confirming it exists elsewhere.
        var parent = await repository.GetByIdAsync(requestedParentId, cancellationToken);
        if (parent is null)
        {
            return Result.Failure(CostCentreErrors.ParentNotFound);
        }

        if (parent.ParentId is not null)
        {
            return Result.Failure(CostCentreErrors.ParentIsNotTopLevel);
        }

        if (!parent.IsActive && requestedParentId != currentParentId)
        {
            return Result.Failure(CostCentreErrors.ParentRetired);
        }

        if (checkChildren && await repository.HasChildrenAsync(selfId, cancellationToken))
        {
            return Result.Failure(CostCentreErrors.HasChildrenCannotHaveParent);
        }

        return Result.Success();
    }
}
