using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Application.Clients;

internal sealed class GetContractsByClientIdQueryHandler(IContractRepository contractRepository)
    : IRequestHandler<GetContractsByClientIdQuery, IReadOnlyList<ContractSummaryResult>>
{
    public async Task<IReadOnlyList<ContractSummaryResult>> Handle(GetContractsByClientIdQuery request, CancellationToken cancellationToken)
    {
        var contracts = await contractRepository.GetByClientIdAsync(request.ClientId, cancellationToken);

        return contracts.Select(c => new ContractSummaryResult(
            c.Id,
            c.StartDate,
            c.RenewalDate,
            c.IsExpiringSoon,
            c.Notes,
            c.RateLines.Select(r => new RateLineResult(
                r.Id,
                r.BillingCode,
                r.Description,
                r.VehicleType,
                r.MaxDistanceKm,
                r.CargoIncluded,
                r.DayRate)).ToList()
        )).ToList();
    }
}
