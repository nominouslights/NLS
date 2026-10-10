using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;

namespace NorthernLink.Budgeting.Application.CostCentres.GetCostCentres;

/// <summary>Handles <see cref="GetCostCentresQuery"/>.</summary>
public sealed class GetCostCentresQueryHandler(ICostCentreReadService readService)
    : IQueryHandler<GetCostCentresQuery, IReadOnlyList<CostCentreResponse>>
{
    public async Task<Result<IReadOnlyList<CostCentreResponse>>> Handle(
        GetCostCentresQuery query, CancellationToken cancellationToken)
    {
        var costCentres = await readService.GetCostCentresAsync(query.IncludeInactive, cancellationToken);
        return Result.Success(costCentres);
    }
}
