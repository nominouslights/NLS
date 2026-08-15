using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Codes;

/// <summary>
/// The one-level hierarchy rule, in one place so create and edit cannot drift apart — the same
/// reason <see cref="BudgetCodeDetails"/> exists. It lives in the application layer rather than
/// on the aggregate because it needs to see the tenant's <em>other</em> codes, which an aggregate
/// by definition cannot.
/// <para>
/// "One level" has to be guarded from both directions. Guarding only from above ("your parent
/// must be top-level") lets a planner build a two-level chain bottom-up: give A a child B, then
/// give A a parent. Hence the children check on the update path.
/// </para>
/// </summary>
public static class BudgetCodeParentRule
{
    /// <param name="selfId">
    /// The code being created or edited. On create this is the not-yet-persisted aggregate's id,
    /// which no caller can know, so the self-check is unreachable there — it is kept anyway
    /// because it costs one comparison and the edit path genuinely needs it.
    /// </param>
    /// <param name="checkChildren">
    /// True on update only. A code being created cannot have children yet, and asking would be a
    /// wasted query on every create.
    /// </param>
    public static async Task<Result> ValidateAsync(
        IBudgetCodeRepository repository,
        Guid? parentCodeId,
        Guid selfId,
        bool checkChildren,
        CancellationToken cancellationToken)
    {
        if (parentCodeId is not { } parentId)
        {
            return Result.Success();
        }

        if (parentId == selfId)
        {
            return Result.Failure(BudgetCodeErrors.ParentIsSelf);
        }

        // There is deliberately no explicit tenant comparison here, and a reviewer will look for
        // one. GetByIdAsync queries through the DbContext's tenant query filter with RLS
        // underneath, so a parent belonging to another tenant reads back as null and reports
        // ParentNotFound — which is also the correct answer: confirming the id exists elsewhere
        // would leak that another tenant owns it.
        var parent = await repository.GetByIdAsync(parentId, cancellationToken);
        if (parent is null)
        {
            return Result.Failure(BudgetCodeErrors.ParentNotFound);
        }

        if (parent.ParentCodeId is not null)
        {
            return Result.Failure(BudgetCodeErrors.ParentIsNotTopLevel);
        }

        if (checkChildren && await repository.HasChildrenAsync(selfId, cancellationToken))
        {
            return Result.Failure(BudgetCodeErrors.CodeWithChildrenCannotHaveParent);
        }

        return Result.Success();
    }
}
