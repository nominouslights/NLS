using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Allocations.Update;

/// <summary>
/// Handles <see cref="UpdateBudgetAllocationCommand"/>. The order of the guards is the contract:
/// <list type="number">
/// <item><b>Validate the input</b> — <see cref="BudgetAllocationErrors.CodeRequired"/>, then
/// <c>BudgetAllocation.Validate</c> — before any lookup.</item>
/// <item><b>The period exists and allows plan changes</b>, exactly as on create.</item>
/// <item><b>The item exists in that period</b> — looked up by (period, id) through the
/// tenant-filtered repository, so an item of another period or another tenant is
/// <see cref="BudgetAllocationErrors.NotFound"/>, never a cross-period or cross-tenant write.</item>
/// <item><b>The target code exists in this period and is active</b> — whether or not it changed.
/// Looked up by (period, id), so another period's code is <see cref="BudgetCodeErrors.NotFound"/>. Moving an item
/// to a retired code is a new decision against a code no longer offered; and so is rewriting an
/// item whose code has been retired since (the rule the one-line-per-code upsert already had).
/// The way out <see cref="BudgetAllocationErrors.CodeRetired"/> names is real: restore the code,
/// or move the item to another code in this same update — or remove it, which is never gated on
/// the code.</item>
/// <item><b>Update in place, one save.</b></item>
/// </list>
/// </summary>
public sealed class UpdateBudgetAllocationCommandHandler(
    IBudgetAllocationRepository allocations,
    IBudgetPeriodRepository periods,
    IBudgetCodeRepository codes)
    : ICommandHandler<UpdateBudgetAllocationCommand>
{
    public async Task<Result> Handle(UpdateBudgetAllocationCommand command, CancellationToken cancellationToken)
    {
        if (command.BudgetCodeId is not { } budgetCodeId)
        {
            return Result.Failure(BudgetAllocationErrors.CodeRequired);
        }

        var validation = BudgetAllocation.Validate(command.Details);
        if (validation.IsFailure)
        {
            return validation;
        }

        var period = await periods.GetByIdAsync(command.PeriodId, cancellationToken);
        if (period is null)
        {
            return Result.Failure(BudgetPeriodErrors.NotFound);
        }

        if (!period.AllowsPlanChanges)
        {
            return Result.Failure(BudgetAllocationErrors.PeriodNotEditable);
        }

        var allocation = await allocations.GetByIdAsync(command.PeriodId, command.AllocationId, cancellationToken);
        if (allocation is null)
        {
            return Result.Failure(BudgetAllocationErrors.NotFound);
        }

        var code = await codes.GetByIdAsync(command.PeriodId, budgetCodeId, cancellationToken);
        if (code is null)
        {
            return Result.Failure(BudgetCodeErrors.NotFound);
        }

        if (!code.IsActive)
        {
            return Result.Failure(BudgetAllocationErrors.CodeRetired);
        }

        var update = allocation.Update(code.Id, code.Code, command.Details, command.ActorId);
        if (update.IsFailure)
        {
            return update;
        }

        await allocations.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
