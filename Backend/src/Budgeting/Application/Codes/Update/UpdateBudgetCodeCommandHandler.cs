using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Codes.Update;

/// <summary>
/// Handles <see cref="UpdateBudgetCodeCommand"/>. The period must exist and be Draft or Open.
/// The repository is keyed on (period, id) and tenant-filtered, so a code belonging to another
/// period or another tenant reads back as null and reports NotFound rather than leaking that the
/// id exists.
/// <para>
/// Unlike create, this passes <c>checkChildren: true</c> to the parent rule: a code that already
/// has codes rolling up into it cannot be given a parent of its own without producing the
/// two-level chain the hierarchy forbids.
/// </para>
/// </summary>
public sealed class UpdateBudgetCodeCommandHandler(
    IBudgetCodeRepository repository,
    IBudgetPeriodRepository periods,
    IUserLookupRepository users,
    ICostCentreRepository costCentres)
    : ICommandHandler<UpdateBudgetCodeCommand>
{
    public async Task<Result> Handle(UpdateBudgetCodeCommand command, CancellationToken cancellationToken)
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

        var parentResult = await BudgetCodeParentRule.ValidateAsync(
            repository, command.PeriodId, command.Details.ParentCodeId, budgetCode.Id, checkChildren: true, cancellationToken);
        if (parentResult.IsFailure)
        {
            return parentResult;
        }

        var ownerResult = await BudgetOwnerRule.ValidateAsync(
            users, command.Details.BudgetOwnerUserId, cancellationToken);
        if (ownerResult.IsFailure)
        {
            return ownerResult;
        }

        // The code's stored value is passed so an unchanged cost centre is accepted even if it has
        // since been retired in the register — retiring an entry must not freeze existing codes.
        // A changed value is checked under the register's (tenant, code) lock, held through the
        // commit, so a concurrent cost-centre delete cannot slip between the lookup and the save.
        await using var costCentreLock = await BudgetCodeCostCentreRule.LockIfCheckedAsync(
            costCentres, command.TenantId, command.Details, budgetCode.CostCentre, cancellationToken);

        var costCentreResult = await BudgetCodeCostCentreRule.ValidateAsync(
            costCentres, command.Details, budgetCode.CostCentre, cancellationToken);
        if (costCentreResult.IsFailure)
        {
            return costCentreResult;
        }

        var result = budgetCode.Update(command.Details, command.ActorId);
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        if (costCentreLock is not null)
        {
            await costCentreLock.CommitAsync(cancellationToken);
        }

        return Result.Success();
    }
}
