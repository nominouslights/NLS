using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Codes.SetActive;

/// <summary>
/// Handles <see cref="SetBudgetCodeActiveCommand"/>. Idempotent: asking for the state a code is
/// already in changes nothing and still succeeds, so a repeated click is not an error.
/// <para>
/// Retiring a parent does <b>not</b> cascade to its children. Retirement is a statement about
/// one code's availability for new work, and silently retiring a branch would hide codes a
/// planner never touched.
/// </para>
/// </summary>
public sealed class SetBudgetCodeActiveCommandHandler(IBudgetCodeRepository repository)
    : ICommandHandler<SetBudgetCodeActiveCommand>
{
    public async Task<Result> Handle(SetBudgetCodeActiveCommand command, CancellationToken cancellationToken)
    {
        var budgetCode = await repository.GetByIdAsync(command.BudgetCodeId, cancellationToken);
        if (budgetCode is null)
        {
            return Result.Failure(BudgetCodeErrors.NotFound);
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
