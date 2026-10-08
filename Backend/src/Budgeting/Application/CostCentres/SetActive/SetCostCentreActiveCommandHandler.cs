using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Application.CostCentres.SetActive;

/// <summary>
/// Handles <see cref="SetCostCentreActiveCommand"/>. Asking for the state the entry is already in
/// is a success that changes nothing.
/// <para>
/// <b>Retiring a parent that still has active children is refused</b>
/// (<see cref="CostCentreErrors.HasActiveChildren"/>, 409) rather than cascaded: silently
/// retiring a branch would hide units a planner never touched, and leaving active children under
/// a retired parent would make the rollup's hierarchy lie. Restoring a child under a retired
/// parent is allowed — restoring is never the harmful direction, and the parent can be restored
/// too.
/// </para>
/// </summary>
public sealed class SetCostCentreActiveCommandHandler(ICostCentreRepository repository)
    : ICommandHandler<SetCostCentreActiveCommand>
{
    public async Task<Result> Handle(SetCostCentreActiveCommand command, CancellationToken cancellationToken)
    {
        var costCentre = await repository.GetByIdAsync(command.CostCentreId, cancellationToken);
        if (costCentre is null)
        {
            return Result.Failure(CostCentreErrors.NotFound);
        }

        if (!command.IsActive
            && costCentre.IsActive
            && await repository.HasActiveChildrenAsync(costCentre.Id, cancellationToken))
        {
            return Result.Failure(CostCentreErrors.HasActiveChildren);
        }

        var result = costCentre.SetActive(command.IsActive, command.ActorId);
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
