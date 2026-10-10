using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Codes;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Application.CostCentres.Update;

/// <summary>
/// Handles <see cref="UpdateCostCentreCommand"/>. The entry is looked up through the
/// tenant-filtered repository (another tenant's id is NotFound), then the code-immutability
/// guard, the parent rule (with <c>checkChildren: true</c> — a parent cannot take a parent) and
/// the owner rule, and finally the aggregate's own validation. An edit that changes nothing
/// raises no event.
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
