using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Codes;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Application.CostCentres.Create;

/// <summary>
/// Creates a cost centre after enforcing per-tenant uniqueness of the (trimmed, ordinal) code.
/// The order is the budget-code create handler's: domain validation first, then the cross-row
/// lookups (duplicate, parent, owner), so a malformed payload reports its validation error.
/// <para>
/// <b>The lookups run under the register lock</b> (<see cref="ICostCentreRepository.LockRegisterAsync"/>),
/// taken after the pure validation and before the first register read, and committed after the
/// save. The parent check is what needs it: without it the chosen parent could be retired
/// (<c>SetCostCentreActiveCommandHandler</c>) or deleted (<c>DeleteCostCentreCommandHandler</c>)
/// between this check and the insert, leaving an active child under a retired parent or an
/// orphan. Both of those writes check this entry's existence under the same lock.
/// </para>
/// <para>
/// The unique (tenant_id, code) index stays as the backstop for a writer that bypasses the lock,
/// and losing that race is the same <see cref="CostCentreErrors.DuplicateCode"/> 409 as the
/// pre-check (<see cref="ICostCentreRepository.TrySaveChangesAsync"/>), never a 500. There is no
/// re-read of the winner: the DuplicateCode message is fixed text that names nothing.
/// </para>
/// </summary>
public sealed class CreateCostCentreCommandHandler(
    ICostCentreRepository repository,
    IUserLookupRepository users)
    : ICommandHandler<CreateCostCentreCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCostCentreCommand command, CancellationToken cancellationToken)
    {
        var created = CostCentre.Create(command.TenantId, command.Code, command.Details, command.ActorId);
        if (created.IsFailure)
        {
            return Result.Failure<Guid>(created.Error);
        }

        var costCentre = created.Value;

        await using var registerLock = await repository.LockRegisterAsync(command.TenantId, cancellationToken);

        if (await repository.GetByCodeAsync(costCentre.Code, cancellationToken) is not null)
        {
            return Result.Failure<Guid>(CostCentreErrors.DuplicateCode);
        }

        // currentParentId: null and checkChildren: false — a new entry has neither.
        var parentResult = await CostCentreParentRule.ValidateAsync(
            repository,
            command.Details.ParentId,
            costCentre.Id,
            currentParentId: null,
            checkChildren: false,
            cancellationToken);
        if (parentResult.IsFailure)
        {
            return Result.Failure<Guid>(parentResult.Error);
        }

        var ownerResult = await BudgetOwnerRule.ValidateAsync(
            users, command.Details.OwnerUserId, CostCentreErrors.OwnerNotFound, cancellationToken);
        if (ownerResult.IsFailure)
        {
            return Result.Failure<Guid>(ownerResult.Error);
        }

        repository.Add(costCentre);

        // Disposing the lock scope without committing rolls back the (already failed) transaction.
        if (!await repository.TrySaveChangesAsync(cancellationToken))
        {
            return Result.Failure<Guid>(CostCentreErrors.DuplicateCode);
        }

        await registerLock.CommitAsync(cancellationToken);
        return Result.Success(costCentre.Id);
    }
}
