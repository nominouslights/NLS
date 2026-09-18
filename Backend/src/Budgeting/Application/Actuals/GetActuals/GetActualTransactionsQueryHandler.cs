using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Actuals.GetActuals;

/// <summary>
/// Handles <see cref="GetActualTransactionsQuery"/>. The period lookup does double duty: it turns
/// a wrong id into a 404 rather than an empty list that looks like a period nobody has spent
/// against — very different things on a dashboard — and it supplies the inclusive date range that
/// <em>is</em> the definition of "this period's transactions", since no row stores a period id.
/// Both reads go to the read side; the period check costs one indexed row.
/// </summary>
public sealed class GetActualTransactionsQueryHandler(
    IActualTransactionReadService transactions,
    IBudgetPeriodReadService periods)
    : IQueryHandler<GetActualTransactionsQuery, IReadOnlyList<ActualTransactionResponse>>
{
    public async Task<Result<IReadOnlyList<ActualTransactionResponse>>> Handle(
        GetActualTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        var period = await periods.GetPeriodAsync(query.PeriodId, cancellationToken);
        if (period is null)
        {
            return Result.Failure<IReadOnlyList<ActualTransactionResponse>>(BudgetPeriodErrors.NotFound);
        }

        var rows = await transactions.GetForDateRangeAsync(
            period.StartsOn, period.EndsOn, cancellationToken);

        return Result.Success(rows);
    }
}
