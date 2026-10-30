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
/// <item><b>Any vendor the update adds exists and is active</b> (<see cref="BudgetItemVendorRule"/>),
/// under the vendor register lock. Shares the item already had are not looked up — a share on a
/// vendor retired since stays valid — and omitted (null) shares keep the current ones.</item>
/// <item><b>Update in place, one save</b> — the aggregate re-checks the shares against the new
/// amount (an amount change the shares no longer sum to is refused, never rebalanced) and applies
/// the category rules on change.</item>
/// </list>
/// </summary>
public sealed class UpdateBudgetAllocationCommandHandler(
    IBudgetAllocationRepository allocations,
    IBudgetPeriodRepository periods,
    IBudgetCodeRepository codes,
    IVendorRepository vendors)
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

        await using var vendorLock = await BudgetItemVendorRule.LockIfAddingAsync(
            vendors, command.TenantId, command.Details, allocation.VendorShares, code.Category, cancellationToken);

        var vendorCheck = await BudgetItemVendorRule.ValidateAsync(
            vendors, command.Details, allocation.VendorShares, code.Category, cancellationToken);
        if (vendorCheck.IsFailure)
        {
            return vendorCheck;
        }

        var update = allocation.Update(code.Id, code.Code, code.Category, command.Details, command.ActorId);
        if (update.IsFailure)
        {
            return update;
        }

        await allocations.SaveChangesAsync(cancellationToken);
        if (vendorLock is not null)
        {
            await vendorLock.CommitAsync(cancellationToken);
        }

        return Result.Success();
    }
}
