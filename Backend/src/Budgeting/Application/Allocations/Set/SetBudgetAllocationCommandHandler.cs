using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Allocations.Set;

/// <summary>
/// Handles <see cref="SetBudgetAllocationCommand"/>. The order of the guards is the contract:
/// <list type="number">
/// <item><b>Validate the input</b> before any lookup — a malformed payload reports its
/// validation error, not a not-found for a period it was never going to reach.</item>
/// <item><b>The period exists</b> (tenant-filtered, so another tenant's id reads as
/// NotFound) <b>and allows plan changes</b> — Draft or Open; anything else is
/// <see cref="BudgetAllocationErrors.PeriodNotEditable"/>.</item>
/// <item><b>The code exists and is active.</b> A retired code stays listed so old lines keep
/// resolving, but it takes no new plan — <see cref="BudgetAllocationErrors.CodeRetired"/> names
/// the way out (restore it or pick another). An <em>existing</em> line on a code retired since is
/// refused too: rewriting it is a new decision against a code no longer offered.</item>
/// <item><b>Upsert.</b> A line for the pair already exists ⇒ <c>Update</c> in place; otherwise
/// <c>Create</c> + <c>Add</c>. One save either way.</item>
/// </list>
/// Two first-time sets racing for the same pair both take the create branch; the unique
/// (tenant, period, code) index kills the second, the same backstop codes and periods rely on.
/// </summary>
public sealed class SetBudgetAllocationCommandHandler(
    IBudgetAllocationRepository allocations,
    IBudgetPeriodRepository periods,
    IBudgetCodeRepository codes)
    : ICommandHandler<SetBudgetAllocationCommand, BudgetAllocationSetResult>
{
    public async Task<Result<BudgetAllocationSetResult>> Handle(
        SetBudgetAllocationCommand command,
        CancellationToken cancellationToken)
    {
        var validation = BudgetAllocation.Validate(command.AmountCad, command.Justification);
        if (validation.IsFailure)
        {
            return Result.Failure<BudgetAllocationSetResult>(validation.Error);
        }

        var period = await periods.GetByIdAsync(command.PeriodId, cancellationToken);
        if (period is null)
        {
            return Result.Failure<BudgetAllocationSetResult>(BudgetPeriodErrors.NotFound);
        }

        if (!period.AllowsPlanChanges)
        {
            return Result.Failure<BudgetAllocationSetResult>(BudgetAllocationErrors.PeriodNotEditable);
        }

        var code = await codes.GetByIdAsync(command.BudgetCodeId, cancellationToken);
        if (code is null)
        {
            return Result.Failure<BudgetAllocationSetResult>(BudgetCodeErrors.NotFound);
        }

        if (!code.IsActive)
        {
            return Result.Failure<BudgetAllocationSetResult>(BudgetAllocationErrors.CodeRetired);
        }

        var existing = await allocations.GetAsync(command.PeriodId, command.BudgetCodeId, cancellationToken);
        if (existing is not null)
        {
            var update = existing.Update(command.AmountCad, command.Justification, command.ActorId);
            if (update.IsFailure)
            {
                return Result.Failure<BudgetAllocationSetResult>(update.Error);
            }

            await allocations.SaveChangesAsync(cancellationToken);
            return Result.Success(new BudgetAllocationSetResult(existing.Id, Created: false));
        }

        var created = BudgetAllocation.Create(
            command.TenantId,
            command.PeriodId,
            code.Id,
            code.Code,
            command.AmountCad,
            command.Justification,
            command.ActorId);

        if (created.IsFailure)
        {
            return Result.Failure<BudgetAllocationSetResult>(created.Error);
        }

        allocations.Add(created.Value);
        await allocations.SaveChangesAsync(cancellationToken);
        return Result.Success(new BudgetAllocationSetResult(created.Value.Id, Created: true));
    }
}
