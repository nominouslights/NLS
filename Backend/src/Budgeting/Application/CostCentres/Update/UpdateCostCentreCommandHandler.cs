using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Codes;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Application.CostCentres.Update;

/// <summary>
/// Handles <see cref="UpdateCostCentreCommand"/>. The entry is looked up through the
/// tenant-filtered repository (another tenant's id is NotFound). Then come the payload's own
/// checks: the code-immutability guard and the aggregate's field validation
/// (<see cref="CostCentre.Validate"/>). The cross-row lookups run last: the parent rule (with
/// <c>checkChildren: true</c>, because a parent cannot take a parent) and then the owner rule.
/// That is the create handler's order and the <c>Invalid_details_report_validation_not_conflict</c>
/// rule: a malformed payload reports its validation error, never a not-found or conflict for a
/// lookup it was never going to pass. An edit that changes nothing raises no event.
/// </summary>
public sealed class UpdateCostCentreCommandHandler(
    ICostCentreRepository repository,
    IUserLookupRepository users)
    : ICommandHandler<UpdateCostCentreCommand>
{
    public async Task<Result> Handle(UpdateCostCentreCommand command, CancellationToken cancellationToken)
    {
        var costCentre = await repository.GetByIdAsync(command.CostCentreId, cancellationToken);
        if (costCentre is null)
        {
            return Result.Failure(CostCentreErrors.NotFound);
        }

        var requestedCode = CostCentre.NormalizeCode(command.Code);
        if (requestedCode.Length > 0 && !string.Equals(requestedCode, costCentre.Code, StringComparison.Ordinal))
        {
            return Result.Failure(CostCentreErrors.CodeImmutable);
        }

        var validation = CostCentre.Validate(command.Details);
        if (validation.IsFailure)
        {
            return validation;
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
        return Result.Success();
    }
}
