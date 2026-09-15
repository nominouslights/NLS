using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Periods.Transition;

/// <summary>
/// Handles <see cref="TransitionBudgetPeriodCommand"/>. The repository is tenant-filtered, so
/// another tenant's period reads back as null and reports NotFound rather than leaking that the
/// id exists. A refused step (wrong source state) saves nothing.
/// </summary>
public sealed class TransitionBudgetPeriodCommandHandler(IBudgetPeriodRepository repository)
    : ICommandHandler<TransitionBudgetPeriodCommand>
{
    public async Task<Result> Handle(TransitionBudgetPeriodCommand command, CancellationToken cancellationToken)
    {
        var period = await repository.GetByIdAsync(command.PeriodId, cancellationToken);
        if (period is null)
        {
            return Result.Failure(BudgetPeriodErrors.NotFound);
        }

        var result = period.Transition(command.Transition, command.ActorId);
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
