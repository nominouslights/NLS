using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Vendors.GetPlannedRollup;

/// <summary>
/// Handles <see cref="GetVendorRollupQuery"/>. Checks the period exists first so a wrong id is a
/// 404 rather than a rollup of zeroes that looks like an empty plan (the
/// <c>GetCostCentreRollupQueryHandler</c> precedent). Readable in every period state.
/// </summary>
public sealed class GetVendorRollupQueryHandler(
    IVendorReadService readService,
    IBudgetPeriodReadService periods)
    : IQueryHandler<GetVendorRollupQuery, VendorRollupResponse>
{
    public async Task<Result<VendorRollupResponse>> Handle(
        GetVendorRollupQuery query, CancellationToken cancellationToken)
    {
        if (await periods.GetPeriodAsync(query.PeriodId, cancellationToken) is null)
        {
            return Result.Failure<VendorRollupResponse>(BudgetPeriodErrors.NotFound);
        }

        var rollup = await readService.GetPlannedRollupAsync(query.PeriodId, cancellationToken);
        return Result.Success(rollup);
    }
}
