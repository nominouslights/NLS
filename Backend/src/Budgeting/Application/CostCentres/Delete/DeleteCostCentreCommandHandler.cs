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
/// <b>The usage probe and the delete are atomic with respect to new usage.</b> Both run inside
/// <see cref="ICostCentreRepository.LockCodeAsync"/>, the (tenant, code) lock that a budget-code
/// create or edit also takes before it validates a newly chosen cost centre against the register
/// (<see cref="Codes.BudgetCodeCostCentreRule.LockIfCheckedAsync"/>). Whichever request gets the
/// lock first wins, and the other one sees its outcome. If the budget code commits first, this
/// probe sees the code and reports InUse. If this delete commits first, the budget code's
/// register lookup finds nothing and reports CostCentreNotFound. Without the lock the probe and
/// the delete could interleave with that write and leave a code carrying a string no register
/// entry matches.
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
        var costCentre = await repository.GetByIdAsync(command.CostCentreId, cancellationToken);
        if (costCentre is null)
        {
            return Result.Failure(CostCentreErrors.NotFound);
        }

        if (await repository.HasChildrenAsync(costCentre.Id, cancellationToken))
        {
            return Result.Failure(CostCentreErrors.HasChildren);
        }

        await using var codeLock = await repository.LockCodeAsync(command.TenantId, costCentre.Code, cancellationToken);

        // Probed under the lock: no budget code can start carrying this string until we commit.
        if (await usageProbe.IsReferencedAsync(costCentre.Code, cancellationToken))
        {
            return Result.Failure(CostCentreErrors.InUse);
        }

        repository.Remove(costCentre);
        await repository.SaveChangesAsync(cancellationToken);
        await codeLock.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
