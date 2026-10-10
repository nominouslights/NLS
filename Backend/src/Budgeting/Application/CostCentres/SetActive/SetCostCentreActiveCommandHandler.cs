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
/// </para>
/// <para>
/// <b>The two directions are only exclusive because both run under the register lock</b>
/// (<see cref="ICostCentreRepository.LockRegisterAsync"/>), taken before the entry is read and
/// committed after the save. Each check reads a <em>different</em> row from the one it writes:
/// restoring a child reads the parent, retiring a parent reads the children. Without the lock a
/// restore and a retirement could each pass their check against the other's pre-commit state and
/// both commit. Under it, whichever request runs second sees the first one's outcome and is
/// refused. The same lock covers a create or edit choosing a parent
/// (<see cref="CostCentreParentRule"/>), so a retirement cannot slip in after that check either.
/// Because all of this holds, an <em>active</em> child never has a retired parent, and the parent
/// rule's "keep an existing retired parent" leniency on edit only ever applies to a retired child.
/// </para>
/// </summary>
public sealed class SetCostCentreActiveCommandHandler(ICostCentreRepository repository)
    : ICommandHandler<SetCostCentreActiveCommand>
{
    public async Task<Result> Handle(SetCostCentreActiveCommand command, CancellationToken cancellationToken)
    {
        await using var registerLock = await repository.LockRegisterAsync(command.TenantId, cancellationToken);

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
        await registerLock.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
