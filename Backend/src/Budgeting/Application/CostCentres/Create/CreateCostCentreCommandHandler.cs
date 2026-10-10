using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Codes;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Application.CostCentres.Create;

/// <summary>
/// Creates a cost centre after enforcing per-tenant uniqueness of the (trimmed, ordinal) code.
/// The order is the budget-code create handler's: domain validation first, then the cross-row
/// lookups (duplicate, parent, owner), so a malformed payload reports its validation error. The
/// unique (tenant_id, code) index is the double-click backstop.
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
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(costCentre.Id);
    }
}
