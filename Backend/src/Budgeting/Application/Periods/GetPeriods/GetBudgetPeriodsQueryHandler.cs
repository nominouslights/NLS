using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;

namespace NorthernLink.Budgeting.Application.Periods.GetPeriods;

/// <summary>Handles <see cref="GetBudgetPeriodsQuery"/>.</summary>
public sealed class GetBudgetPeriodsQueryHandler(IBudgetPeriodReadService readService)
    : IQueryHandler<GetBudgetPeriodsQuery, IReadOnlyList<BudgetPeriodResponse>>
{
    public async Task<Result<IReadOnlyList<BudgetPeriodResponse>>> Handle(
        GetBudgetPeriodsQuery query,
        CancellationToken cancellationToken)
    {
        var periods = await readService.GetPeriodsAsync(cancellationToken);
        return Result.Success(periods);
    }
}
