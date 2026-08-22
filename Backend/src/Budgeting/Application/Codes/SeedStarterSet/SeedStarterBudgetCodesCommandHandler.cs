using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Codes.SeedStarterSet;

/// <summary>
/// Handles <see cref="SeedStarterBudgetCodesCommand"/>.
/// <para>
/// This runs through the ordinary aggregate path — <c>BudgetCode.Create</c>, the repository, one
/// <c>SaveChangesAsync</c> — rather than inserting rows, and that is the whole reason it is a
/// command instead of a migration. Raw SQL would bypass <c>AppendAuditEntries</c>, so no
/// event_journal row would be written, so the projection worker would never run, so
/// <c>rm_budget_codes</c> would stay empty and <c>GET /api/budgeting/codes</c> would return
/// nothing while the write table quietly held twelve codes. Going through the aggregate also
/// means the seeded rows carry <c>created_by</c> and land under the caller's tenant from the JWT,
/// so this works unchanged the day a second tenant exists.
/// </para>
/// <para>
/// Skips rather than fails on a collision: a tenant that already hand-created ZBB-FUEL-01 keeps
/// theirs. The count returned is of codes actually created.
/// </para>
/// </summary>
public sealed class SeedStarterBudgetCodesCommandHandler(IBudgetCodeRepository repository)
    : ICommandHandler<SeedStarterBudgetCodesCommand, int>
{
    public async Task<Result<int>> Handle(
        SeedStarterBudgetCodesCommand command, CancellationToken cancellationToken)
    {
        var created = 0;

        foreach (var seed in StarterBudgetCodes.All)
        {
            var normalized = BudgetCode.NormalizeCode(seed.Code);
            if (await repository.GetByCodeAsync(normalized, cancellationToken) is not null)
            {
                continue;
            }

            var details = new BudgetCodeDetails
            {
                Name = seed.Name,
                Category = seed.Category,
                ServiceLine = seed.ServiceLine,
                Description = seed.Description,
                ReviewFrequency = BudgetReviewFrequency.Quarterly,
            };

            var result = BudgetCode.Create(command.TenantId, seed.Code, details, command.ActorId);
            if (result.IsFailure)
            {
                // A malformed entry in the static table is a programming error, not a user one —
                // surface it rather than silently seeding eleven of twelve.
                return Result.Failure<int>(result.Error);
            }

            repository.Add(result.Value);
            created++;
        }

        if (created > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(created);
    }
}
