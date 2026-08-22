using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;

namespace NorthernLink.Budgeting.Application.Codes.GetCodes;

/// <summary>Handles <see cref="GetBudgetCodesQuery"/>.</summary>
public sealed class GetBudgetCodesQueryHandler(IBudgetCodeReadService readService)
    : IQueryHandler<GetBudgetCodesQuery, IReadOnlyList<BudgetCodeResponse>>
{
    public async Task<Result<IReadOnlyList<BudgetCodeResponse>>> Handle(
        GetBudgetCodesQuery query,
        CancellationToken cancellationToken)
    {
        var codes = await readService.GetCodesAsync(cancellationToken);
        return Result.Success(codes);
    }
}
