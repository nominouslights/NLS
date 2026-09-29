using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Allocations.Create;

/// <summary>
/// Handles <see cref="CreateBudgetAllocationCommand"/>. The order of the guards is the contract:
/// <list type="number">
/// <item><b>Validate the input</b> before any lookup — a code must be named
/// (<see cref="BudgetAllocationErrors.CodeRequired"/>), then <c>BudgetAllocation.Validate</c>. A
/// malformed payload reports its validation error, not a not-found for a period it was never
/// going to reach.</item>
/// <item><b>The period exists</b> (tenant-filtered, so another tenant's id reads as NotFound)
/// <b>and allows plan changes</b> — Draft or Open; anything else is
/// <see cref="BudgetAllocationErrors.PeriodNotEditable"/>.</item>
/// <item><b>The code exists and is active.</b> A retired code stays listed so old items keep
/// resolving, but it takes no new item — <see cref="BudgetAllocationErrors.CodeRetired"/> names
/// the way out (restore it or pick another).</item>
/// <item><b>Create + Add, one save.</b> No "already planned" check: many items per code is the
/// model, and nothing in the schema is unique on (period, code) any more.</item>
/// </list>
/// </summary>
public sealed class CreateBudgetAllocationCommandHandler(
    IBudgetAllocationRepository allocations,
    IBudgetPeriodRepository periods,
    IBudgetCodeRepository codes)
    : ICommandHandler<CreateBudgetAllocationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateBudgetAllocationCommand command,
        CancellationToken cancellationToken)
    {
        if (command.BudgetCodeId is not { } budgetCodeId)
        {
            return Result.Failure<Guid>(BudgetAllocationErrors.CodeRequired);
        }

        var validation = BudgetAllocation.Validate(command.Details);
        if (validation.IsFailure)
        {
            return Result.Failure<Guid>(validation.Error);
        }

        var period = await periods.GetByIdAsync(command.PeriodId, cancellationToken);
        if (period is null)
        {
            return Result.Failure<Guid>(BudgetPeriodErrors.NotFound);
        }

        if (!period.AllowsPlanChanges)
        {
            return Result.Failure<Guid>(BudgetAllocationErrors.PeriodNotEditable);
        }

        var code = await codes.GetByIdAsync(budgetCodeId, cancellationToken);
        if (code is null)
        {
            return Result.Failure<Guid>(BudgetCodeErrors.NotFound);
        }

        if (!code.IsActive)
        {
            return Result.Failure<Guid>(BudgetAllocationErrors.CodeRetired);
        }

        var created = BudgetAllocation.Create(
            command.TenantId,
            command.PeriodId,
            code.Id,
            code.Code,
            command.Details,
            command.ActorId);

        if (created.IsFailure)
        {
            return Result.Failure<Guid>(created.Error);
        }

        allocations.Add(created.Value);
        await allocations.SaveChangesAsync(cancellationToken);
        return Result.Success(created.Value.Id);
    }
}
