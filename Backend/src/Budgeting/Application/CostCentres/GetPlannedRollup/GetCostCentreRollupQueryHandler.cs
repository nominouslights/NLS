using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.CostCentres.GetPlannedRollup;

/// <summary>
/// Handles <see cref="GetCostCentreRollupQuery"/>. Checks the period exists first so a wrong id
/// is a 404 rather than a rollup of zeroes that looks like an empty plan (the
/// <c>GetBudgetCodesQueryHandler</c> precedent). Readable in every period state.
/// </summary>
public sealed class GetCostCentreRollupQueryHandler(
    ICostCentreReadService readService,
    IBudgetPeriodReadService periods)
    : IQueryHandler<GetCostCentreRollupQuery, CostCentreRollupResponse>
{
    public async Task<Result<CostCentreRollupResponse>> Handle(
        GetCostCentreRollupQuery query, CancellationToken cancellationToken)
    {
        if (await periods.GetPeriodAsync(query.PeriodId, cancellationToken) is null)
        {
            return Result.Failure<CostCentreRollupResponse>(BudgetPeriodErrors.NotFound);
        }

        var rollup = await readService.GetPlannedRollupAsync(query.PeriodId, cancellationToken);
        return Result.Success(rollup);
    }
}
