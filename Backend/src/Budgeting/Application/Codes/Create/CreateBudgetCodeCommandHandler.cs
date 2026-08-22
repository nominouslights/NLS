using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Codes.Create;

/// <summary>
/// Creates a budget code after enforcing that the tenant's code strings are unique. The lookup
/// runs against the <em>normalized</em> code, so "fleet-maint" collides with "FLEET-MAINT" — two
/// codes that differ only in case would be indistinguishable to the person reading a report.
/// The unique (tenant, code) index is the DB backstop for the double-click race; this check is
/// what turns the race into a readable 409 in every other case.
/// <para>
/// Order matters and is deliberate: domain validation runs first, then the cross-row lookups. A
/// malformed payload reports the validation error rather than a conflict or a not-found for a
/// parent it was never going to reach — the rule
/// <c>Invalid_details_report_validation_not_conflict</c> pins.
/// </para>
/// </summary>
public sealed class CreateBudgetCodeCommandHandler(
    IBudgetCodeRepository repository,
    IUserLookupRepository users)
    : ICommandHandler<CreateBudgetCodeCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateBudgetCodeCommand command, CancellationToken cancellationToken)
    {
        var codeResult = BudgetCode.Create(command.TenantId, command.Code, command.Details, command.ActorId);
        if (codeResult.IsFailure)
        {
            return Result.Failure<Guid>(codeResult.Error);
        }

        var budgetCode = codeResult.Value;

        var existing = await repository.GetByCodeAsync(budgetCode.Code, cancellationToken);
        if (existing is not null)
        {
            return Result.Failure<Guid>(BudgetCodeErrors.DuplicateCode);
        }

        // checkChildren: false — a code that does not exist yet cannot have any.
        var parentResult = await BudgetCodeParentRule.ValidateAsync(
            repository, command.Details.ParentCodeId, budgetCode.Id, checkChildren: false, cancellationToken);
        if (parentResult.IsFailure)
        {
            return Result.Failure<Guid>(parentResult.Error);
        }

        var ownerResult = await BudgetOwnerRule.ValidateAsync(
            users, command.Details.BudgetOwnerUserId, cancellationToken);
        if (ownerResult.IsFailure)
        {
            return Result.Failure<Guid>(ownerResult.Error);
        }

        repository.Add(budgetCode);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(budgetCode.Id);
    }
}
