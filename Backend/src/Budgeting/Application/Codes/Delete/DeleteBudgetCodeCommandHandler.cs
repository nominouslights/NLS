using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Codes.Delete;

/// <summary>
/// Handles <see cref="DeleteBudgetCodeCommand"/>. The period must exist and be Draft or Open
/// (<see cref="BudgetCodePeriodRule"/>), the code must be in that period, and then two guards,
/// both of which must run before anything is removed:
/// <list type="number">
/// <item>
/// <b>Children.</b> There is no database foreign key on <c>parent_code_id</c>, so Postgres will
/// not stop this. A dangling parent id is invisible corruption — a rollup report does not throw,
/// it just quietly omits a branch. Nulling the children out instead would be a multi-aggregate
/// write in one handler, which this codebase does not do.
/// </item>
/// <item>
/// <b>Usage.</b> A code that any item of its period has been tagged with is retired, never
/// deleted. See <see cref="IBudgetCodeUsageProbe"/>.
/// </item>
/// </list>
/// The aggregate needs no <c>Delete()</c> method and raises no event: <c>AppendAuditEntries</c>
/// exempts deletes from the every-write-raises-an-event rule and writes a final snapshot plus a
/// synthetic <c>aggregate-deleted</c> journal row, which is what drives the projection to drop
/// the read row.
/// </summary>
public sealed class DeleteBudgetCodeCommandHandler(
    IBudgetCodeRepository repository,
    IBudgetPeriodRepository periods,
    IBudgetCodeUsageProbe usageProbe)
    : ICommandHandler<DeleteBudgetCodeCommand>
{
    public async Task<Result> Handle(DeleteBudgetCodeCommand command, CancellationToken cancellationToken)
    {
        var periodResult = await BudgetCodePeriodRule.RequireEditableAsync(periods, command.PeriodId, cancellationToken);
        if (periodResult.IsFailure)
        {
            return periodResult;
        }

        var budgetCode = await repository.GetByIdAsync(command.PeriodId, command.BudgetCodeId, cancellationToken);
        if (budgetCode is null)
        {
            return Result.Failure(BudgetCodeErrors.NotFound);
        }

        if (await repository.HasChildrenAsync(command.PeriodId, budgetCode.Id, cancellationToken))
        {
            return Result.Failure(BudgetCodeErrors.ParentHasChildren);
        }

        if (await usageProbe.IsReferencedAsync(budgetCode.PeriodId, budgetCode.Id, budgetCode.Code, cancellationToken))
        {
            return Result.Failure(BudgetCodeErrors.InUse);
        }

        repository.Remove(budgetCode);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
