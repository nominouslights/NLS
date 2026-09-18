using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Actuals.GetRollup;

/// <summary>
/// Handles <see cref="GetBudgetRollupQuery"/>. The period lookup is the 404 <em>and</em> the
/// source of the two halves of the rollup's addressing: the plan is found by the period's id, the
/// spend by its inclusive dates, because no transaction stores a period id.
/// </summary>
public sealed class GetBudgetRollupQueryHandler(
    IBudgetRollupReadService rollup,
    IBudgetPeriodReadService periods)
    : IQueryHandler<GetBudgetRollupQuery, IReadOnlyList<BudgetRollupRowResponse>>
{
    public async Task<Result<IReadOnlyList<BudgetRollupRowResponse>>> Handle(
        GetBudgetRollupQuery query,
        CancellationToken cancellationToken)
    {
        var period = await periods.GetPeriodAsync(query.PeriodId, cancellationToken);
        if (period is null)
        {
            return Result.Failure<IReadOnlyList<BudgetRollupRowResponse>>(BudgetPeriodErrors.NotFound);
        }

        var rows = await rollup.GetForPeriodAsync(
            period.Id, period.StartsOn, period.EndsOn, cancellationToken);

        return Result.Success(rows);
    }
}
