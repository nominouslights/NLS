using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Codes.Update;

/// <summary>
/// Handles <see cref="UpdateBudgetCodeCommand"/>. The repository is tenant-filtered, so a code
/// belonging to another tenant reads back as null and reports NotFound rather than leaking that
/// the id exists.
/// <para>
/// Unlike create, this passes <c>checkChildren: true</c> to the parent rule: a code that already
/// has codes rolling up into it cannot be given a parent of its own without producing the
/// two-level chain the hierarchy forbids.
/// </para>
/// </summary>
public sealed class UpdateBudgetCodeCommandHandler(
    IBudgetCodeRepository repository,
    IUserLookupRepository users)
    : ICommandHandler<UpdateBudgetCodeCommand>
{
    public async Task<Result> Handle(UpdateBudgetCodeCommand command, CancellationToken cancellationToken)
    {
        var budgetCode = await repository.GetByIdAsync(command.BudgetCodeId, cancellationToken);
        if (budgetCode is null)
        {
            return Result.Failure(BudgetCodeErrors.NotFound);
        }

        var parentResult = await BudgetCodeParentRule.ValidateAsync(
            repository, command.Details.ParentCodeId, budgetCode.Id, checkChildren: true, cancellationToken);
        if (parentResult.IsFailure)
        {
            return parentResult;
        }

        var ownerResult = await BudgetOwnerRule.ValidateAsync(
            users, command.Details.BudgetOwnerUserId, cancellationToken);
        if (ownerResult.IsFailure)
        {
            return ownerResult;
        }

        var result = budgetCode.Update(command.Details, command.ActorId);
        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
