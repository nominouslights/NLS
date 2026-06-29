using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;

namespace ShuttleApi.Application.Clients;

internal sealed class UpdateClientBillingRatesCommandHandler(IClientRepository clientRepository)
    : IRequestHandler<UpdateClientBillingRatesCommand, Unit>
{
    public async Task<Unit> Handle(UpdateClientBillingRatesCommand request, CancellationToken cancellationToken)
    {
        var client = await clientRepository.GetByIdAsync(request.ClientId, cancellationToken)
            ?? throw new NotFoundException($"Client {request.ClientId} not found.");

        client.UpdateBillingRates(request.RunRateOneWay, request.RunRateRoundTrip, request.CargoRatePerKg);

        await clientRepository.UpdateAsync(client, cancellationToken);
        return Unit.Value;
    }
}
