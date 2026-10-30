using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Codes.SetActive;

/// <summary>
/// Handles <see cref="SetBudgetCodeActiveCommand"/>. Idempotent: asking for the state a code is
/// already in changes nothing and still succeeds, so a repeated click is not an error.
/// <para>
/// <b>Retiring is guarded; restoring is not.</b> A code can only be retired once every period
/// that uses it is Closed — see <see cref="BudgetCodeRetirementRule"/> for the rule, and for how
/// it composes with the Draft/Open requirement below (a code its own period uses cannot be
/// retired at all). Restoring a code takes nothing away from anyone, so the <c>activate</c>
/// direction carries no check at all.
/// </para>
/// <para>
/// The guard runs only on an actual transition to retired, not on every retire request. Asking a
/// code that is already retired to retire again is the idempotent no-op above, and re-checking it
/// would start refusing a request that changes nothing — including on codes retired before this
/// rule existed.
/// </para>
/// <para>
/// Retiring a parent does <b>not</b> cascade to its children. Retirement is a statement about
/// one code's availability for new work, and silently retiring a branch would hide codes a
/// planner never touched. (It follows that the rule above is not checked for the children
/// either — nothing happens to them.)
/// </para>
/// <para>
/// Retirement is per period: retiring FUEL in Q4 leaves Q3's FUEL (another row) untouched, and
/// is refused outright unless the period is Draft or Open.
/// </para>
/// </summary>
public sealed class SetBudgetCodeActiveCommandHandler(
    IBudgetCodeRepository repository,
    IBudgetAllocationRepository allocations,
    IActualTransactionRepository actuals,
    IBudgetPeriodRepository periods)
    : ICommandHandler<SetBudgetCodeActiveCommand>
{
    public async Task<Result> Handle(SetBudgetCodeActiveCommand command, CancellationToken cancellationToken)
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

        var isRetiring = !command.IsActive && budgetCode.IsActive;
        if (isRetiring)
        {
            var retirable = await BudgetCodeRetirementRule.ValidateAsync(
                allocations, actuals, periods, budgetCode.PeriodId, budgetCode.Id, budgetCode.Code, cancellationToken);

            if (retirable.IsFailure)
            {
                return retirable;
            }
        }

        var result = budgetCode.SetActive(command.IsActive, command.ActorId);
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
