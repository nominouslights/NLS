using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Application.Clients;

internal sealed class GetRateLinesByClientQueryHandler(IContractRepository contractRepository)
    : IRequestHandler<GetRateLinesByClientQuery, IReadOnlyList<RateLineResult>>
{
    public async Task<IReadOnlyList<RateLineResult>> Handle(GetRateLinesByClientQuery request, CancellationToken cancellationToken)
    {
        var activeContract = await contractRepository.GetActiveByClientIdAsync(request.ClientId, cancellationToken);
        if (activeContract is null)
            return [];

        return activeContract.RateLines
            .Select(r => new RateLineResult(
                r.Id,
                r.BillingCode,
                r.Description,
                r.VehicleType,
                r.MaxDistanceKm,
                r.CargoIncluded,
                r.DayRate))
            .ToList();
    }
}
