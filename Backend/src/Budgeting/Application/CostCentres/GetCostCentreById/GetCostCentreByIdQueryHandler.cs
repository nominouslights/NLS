using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.CostCentres;

namespace NorthernLink.Budgeting.Application.CostCentres.GetCostCentreById;

/// <summary>Handles <see cref="GetCostCentreByIdQuery"/>: 404 when the tenant has no such entry.</summary>
public sealed class GetCostCentreByIdQueryHandler(ICostCentreReadService readService)
    : IQueryHandler<GetCostCentreByIdQuery, CostCentreResponse>
{
    public async Task<Result<CostCentreResponse>> Handle(
        GetCostCentreByIdQuery query, CancellationToken cancellationToken)
    {
        var costCentre = await readService.GetCostCentreAsync(query.CostCentreId, cancellationToken);
        return costCentre is null
            ? Result.Failure<CostCentreResponse>(CostCentreErrors.NotFound)
            : Result.Success(costCentre);
    }
}
