using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Periods.GetPeriodById;

/// <summary>Handles <see cref="GetBudgetPeriodByIdQuery"/>. A missing (or other tenant's) period is NotFound.</summary>
public sealed class GetBudgetPeriodByIdQueryHandler(IBudgetPeriodReadService readService)
    : IQueryHandler<GetBudgetPeriodByIdQuery, BudgetPeriodResponse>
{
    public async Task<Result<BudgetPeriodResponse>> Handle(
        GetBudgetPeriodByIdQuery query,
        CancellationToken cancellationToken)
    {
        var period = await readService.GetPeriodAsync(query.PeriodId, cancellationToken);

        return period is null
            ? Result.Failure<BudgetPeriodResponse>(BudgetPeriodErrors.NotFound)
            : Result.Success(period);
    }
}
