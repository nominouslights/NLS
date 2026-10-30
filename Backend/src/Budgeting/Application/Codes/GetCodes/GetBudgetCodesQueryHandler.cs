using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Codes.GetCodes;

/// <summary>
/// Handles <see cref="GetBudgetCodesQuery"/>. Checks the period exists first so a wrong id is a
/// 404 rather than an empty list that looks like a period with no chart yet — the
/// <c>GetBudgetAllocationsQueryHandler</c> precedent, for the same reason: the console offers
/// "copy codes / load the starter set" on an empty chart, and must not offer it for a wrong id.
/// Reads are allowed in every period state.
/// </summary>
public sealed class GetBudgetCodesQueryHandler(
    IBudgetCodeReadService readService,
    IBudgetPeriodReadService periods)
    : IQueryHandler<GetBudgetCodesQuery, IReadOnlyList<BudgetCodeResponse>>
{
    public async Task<Result<IReadOnlyList<BudgetCodeResponse>>> Handle(
        GetBudgetCodesQuery query,
        CancellationToken cancellationToken)
    {
        var period = await periods.GetPeriodAsync(query.PeriodId, cancellationToken);
        if (period is null)
        {
            return Result.Failure<IReadOnlyList<BudgetCodeResponse>>(BudgetPeriodErrors.NotFound);
        }

        var codes = await readService.GetCodesAsync(query.PeriodId, cancellationToken);
        return Result.Success(codes);
    }
}
