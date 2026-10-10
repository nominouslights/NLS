using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Codes;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Application.CostCentres.Update;

/// <summary>
/// Handles <see cref="UpdateCostCentreCommand"/>. The aggregate's field validation
/// (<see cref="CostCentre.Validate"/>) runs first, on the payload alone. Then the entry is looked
/// up through the tenant-filtered repository (another tenant's id is NotFound), the
/// code-immutability guard compares the requested code with the stored one, and the cross-row
/// lookups run last: the parent rule (with <c>checkChildren: true</c>, because a parent cannot
/// take a parent) and then the owner rule. That is the create handler's order and the
/// <c>Invalid_details_report_validation_not_conflict</c> rule: a malformed payload reports its
/// validation error, never a not-found or conflict for a lookup it was never going to pass. An
/// edit that changes nothing raises no event.
/// <para>
/// <b>Every database read runs under the register lock</b> (<see cref="ICostCentreRepository.LockRegisterAsync"/>),
/// taken after the payload validation and before the entry itself is read, and committed after
/// the save. The parent check (exists, top-level, active when newly chosen) and the children check
/// (an entry with children cannot take a parent) both read other rows that a concurrent create,
/// edit, retirement or delete could change between the check and this commit. Under the lock,
/// those writes either committed before this request read anything or wait until it commits. A
/// malformed PUT is a 400 without queueing behind other register writes.
/// </para>
/// <para>
/// <b>The code-immutability guard stays after the lock.</b> Only an omitted or blank code can be
/// decided from the payload, and that case passes; any other value has to be compared with the
/// stored code, which is a read of the entry — and the entry read belongs under the lock.
/// </para>
/// </summary>
public sealed class UpdateCostCentreCommandHandler(
    ICostCentreRepository repository,
    IUserLookupRepository users)
    : ICommandHandler<UpdateCostCentreCommand>
{
    public async Task<Result> Handle(UpdateCostCentreCommand command, CancellationToken cancellationToken)
    {
        var validation = CostCentre.Validate(command.Details);
        if (validation.IsFailure)
        {
            return validation;
        }

        await using var registerLock = await repository.LockRegisterAsync(command.TenantId, cancellationToken);

        var costCentre = await repository.GetByIdAsync(command.CostCentreId, cancellationToken);
        if (costCentre is null)
        {
            return Result.Failure(CostCentreErrors.NotFound);
        }

        // Needs the stored code, so it cannot move above the lock — see the class comment.
        var requestedCode = CostCentre.NormalizeCode(command.Code);
        if (requestedCode.Length > 0 && !string.Equals(requestedCode, costCentre.Code, StringComparison.Ordinal))
        {
            return Result.Failure(CostCentreErrors.CodeImmutable);
        }

        var parentResult = await CostCentreParentRule.ValidateAsync(
            repository,
            command.Details.ParentId,
            costCentre.Id,
            currentParentId: costCentre.ParentId,
            checkChildren: true,
            cancellationToken);
        if (parentResult.IsFailure)
        {
            return parentResult;
        }

        var ownerResult = await BudgetOwnerRule.ValidateAsync(
            users, command.Details.OwnerUserId, CostCentreErrors.OwnerNotFound, cancellationToken);
        if (ownerResult.IsFailure)
        {
            return ownerResult;
        }

        var result = costCentre.Update(command.Details, command.ActorId);
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        await registerLock.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
