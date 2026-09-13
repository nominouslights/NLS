using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Allocations.Remove;

/// <summary>
/// Handles <see cref="RemoveBudgetAllocationCommand"/>. Same period guards as the set — the
/// period must exist and allow plan changes — then the line must exist. A retired code is
/// <em>not</em> a guard here: taking a line off a retired code is exactly what a planner cleaning
/// up a plan needs to do. No domain event; <c>AppendAuditEntries</c> exempts deletes and writes
/// the synthetic <c>aggregate-deleted</c> row that drives the projection to drop the read row.
/// </summary>
public sealed class RemoveBudgetAllocationCommandHandler(
    IBudgetAllocationRepository allocations,
    IBudgetPeriodRepository periods)
    : ICommandHandler<RemoveBudgetAllocationCommand>
{
    public async Task<Result> Handle(RemoveBudgetAllocationCommand command, CancellationToken cancellationToken)
    {
        var period = await periods.GetByIdAsync(command.PeriodId, cancellationToken);
        if (period is null)
        {
            return Result.Failure(BudgetPeriodErrors.NotFound);
        }

        if (!period.AllowsPlanChanges)
        {
            return Result.Failure(BudgetAllocationErrors.PeriodNotEditable);
        }

        var allocation = await allocations.GetAsync(command.PeriodId, command.BudgetCodeId, cancellationToken);
        if (allocation is null)
        {
            return Result.Failure(BudgetAllocationErrors.NotFound);
        }

        allocations.Remove(allocation);
        await allocations.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
