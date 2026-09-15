using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Allocations.GetAllocations;

/// <summary>
/// Handles <see cref="GetBudgetAllocationsQuery"/>. Checks the period exists first so a wrong id
/// is a 404 rather than an empty list that looks like an unplanned period — the two mean very
/// different things on a dashboard. Both reads go to the read side; the period check costs one
/// indexed row.
/// </summary>
public sealed class GetBudgetAllocationsQueryHandler(
    IBudgetAllocationReadService allocations,
    IBudgetPeriodReadService periods)
    : IQueryHandler<GetBudgetAllocationsQuery, IReadOnlyList<BudgetAllocationResponse>>
{
    public async Task<Result<IReadOnlyList<BudgetAllocationResponse>>> Handle(
        GetBudgetAllocationsQuery query,
        CancellationToken cancellationToken)
    {
        var period = await periods.GetPeriodAsync(query.PeriodId, cancellationToken);
        if (period is null)
        {
            return Result.Failure<IReadOnlyList<BudgetAllocationResponse>>(BudgetPeriodErrors.NotFound);
        }

        var lines = await allocations.GetForPeriodAsync(query.PeriodId, cancellationToken);
        return Result.Success(lines);
    }
}
