using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;

namespace NorthernLink.Budgeting.Application.Codes.GetOwnerCandidates;

/// <summary>Handles <see cref="GetBudgetOwnerCandidatesQuery"/>.</summary>
public sealed class GetBudgetOwnerCandidatesQueryHandler(IUserLookupRepository users)
    : IQueryHandler<GetBudgetOwnerCandidatesQuery, IReadOnlyList<BudgetOwnerOptionResponse>>
{
    public async Task<Result<IReadOnlyList<BudgetOwnerOptionResponse>>> Handle(
        GetBudgetOwnerCandidatesQuery query,
        CancellationToken cancellationToken)
    {
        var candidates = await users.ListAsync(cancellationToken);

        IReadOnlyList<BudgetOwnerOptionResponse> options = candidates
            .Select(u => new BudgetOwnerOptionResponse(u.UserId, u.Email, u.Role))
            .ToList();

        return Result.Success(options);
    }
}
