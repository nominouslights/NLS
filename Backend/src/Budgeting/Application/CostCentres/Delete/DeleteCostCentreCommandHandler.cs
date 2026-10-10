using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Application.CostCentres.Delete;

/// <summary>
/// Handles <see cref="DeleteCostCentreCommand"/>, with the <c>DeleteBudgetCodeCommandHandler</c>
/// guards, both run before anything is removed:
/// <list type="number">
/// <item><b>Children</b> (<see cref="CostCentreErrors.HasChildren"/>) — there is no foreign key
/// on <c>parent_id</c>, so a dangling parent id would be invisible corruption.</item>
/// <item><b>Usage</b> (<see cref="CostCentreErrors.InUse"/>) — any budget code in any period
/// carrying the code string (<see cref="ICostCentreUsageProbe"/>). A used entry is retired,
/// never deleted.</item>
/// </list>
/// <para>
/// <b>Both guards and the delete are atomic with respect to new children and new usage.</b>
/// Everything, from reading the entry onwards, runs under the register lock
/// (<see cref="ICostCentreRepository.LockRegisterAsync"/>), committed with the delete. Every write
/// that could invalidate a guard takes the same lock before its own checks: a cost-centre create
/// or edit choosing this entry as parent (<see cref="CostCentreParentRule"/>), a budget-code create
/// or cost-centre change validating this string
/// (<see cref="Codes.BudgetCodeCostCentreRule.LockIfCheckedAsync"/>), and a chart copy carrying it
/// (<c>CopyBudgetCodesCommandHandler</c>). Whichever request gets the lock first wins, and the
/// other one sees its outcome. If the child or budget code commits first, this delete reports
/// HasChildren or InUse. If this delete commits first, their lookup finds nothing and reports
/// not-found.
/// </para>
/// No <c>Delete()</c> on the aggregate and no event: the audit pipeline writes a final snapshot
/// and the synthetic <c>aggregate-deleted</c> journal row, which drops the read row.
/// </summary>
public sealed class DeleteCostCentreCommandHandler(
    ICostCentreRepository repository,
    ICostCentreUsageProbe usageProbe)
    : ICommandHandler<DeleteCostCentreCommand>
{
    public async Task<Result> Handle(DeleteCostCentreCommand command, CancellationToken cancellationToken)
    {
        await using var registerLock = await repository.LockRegisterAsync(command.TenantId, cancellationToken);

        var costCentre = await repository.GetByIdAsync(command.CostCentreId, cancellationToken);
        if (costCentre is null)
        {
            return Result.Failure(CostCentreErrors.NotFound);
        }

        if (await repository.HasChildrenAsync(costCentre.Id, cancellationToken))
        {
            return Result.Failure(CostCentreErrors.HasChildren);
        }

        if (await usageProbe.IsReferencedAsync(costCentre.Code, cancellationToken))
        {
            return Result.Failure(CostCentreErrors.InUse);
        }

        repository.Remove(costCentre);
        await repository.SaveChangesAsync(cancellationToken);
        await registerLock.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
