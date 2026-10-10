using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Application.CostCentres.SetActive;

/// <summary>
/// Handles <see cref="SetCostCentreActiveCommand"/>. Asking for the state the entry is already in
/// is a success that changes nothing.
/// <para>
/// <b>No active child under a retired parent, guarded from both directions</b>, so the rollup's
/// hierarchy never shows a live unit hanging off a dead one:
/// <list type="bullet">
/// <item><b>Retiring a parent that still has active children is refused</b>
/// (<see cref="CostCentreErrors.HasActiveChildren"/>, 409) rather than cascaded: silently retiring
/// a branch would hide units a planner never touched.</item>
/// <item><b>Restoring a child whose parent is retired is refused</b>
/// (<see cref="CostCentreErrors.ParentRetired"/>, 409, the same error and fix as choosing a
/// retired parent: restore the parent first, or move the child). A parent id that resolves to
/// nothing does not block the restore. Delete refuses to orphan children, so that state only
/// comes from a hand-written row, and refusing would leave the child unrestorable.</item>
/// </list>
/// Because both directions hold, an <em>active</em> child never has a retired parent. The parent
/// rule's "keep an existing retired parent" leniency on edit
/// (<see cref="CostCentreParentRule"/>) therefore only ever applies to a retired child.
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

        if (command.IsActive == costCentre.IsActive)
        {
            return Result.Success();
        }

        if (!command.IsActive
            && await repository.HasActiveChildrenAsync(costCentre.Id, cancellationToken))
        {
            return Result.Failure(CostCentreErrors.HasActiveChildren);
        }

        if (command.IsActive
            && costCentre.ParentId is { } parentId
            && await repository.GetByIdAsync(parentId, cancellationToken) is { IsActive: false })
        {
            return Result.Failure(CostCentreErrors.ParentRetired);
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
