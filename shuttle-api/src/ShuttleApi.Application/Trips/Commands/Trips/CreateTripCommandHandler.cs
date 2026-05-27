using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class CreateTripCommandHandler(
    ITripRepository tripRepository,
    IClientRepository clientRepository)
    : IRequestHandler<CreateTripCommand, CreateTripResult>
{
    public async Task<CreateTripResult> Handle(CreateTripCommand request, CancellationToken cancellationToken)
    {
        var client = await clientRepository.GetByIdAsync(request.ClientId, cancellationToken)
            ?? throw new NotFoundException($"Client {request.ClientId} not found.");

        var stops = request.Stops.Select(s => (s.SequenceOrder, s.LocationName, s.Address));

        var trip = Trip.Create(
            client.Id,
            request.PurchaseOrderNumber,
            request.VehicleType,
            DateTime.SpecifyKind(request.ScheduledAt, DateTimeKind.Utc),
            request.Notes,
            stops);

        await tripRepository.AddAsync(trip, cancellationToken);

        return new CreateTripResult(trip.Id);
    }
}
