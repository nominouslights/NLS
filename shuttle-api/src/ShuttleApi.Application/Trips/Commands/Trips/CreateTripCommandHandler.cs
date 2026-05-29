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
        Guid? clientId = null;

        if (request.ServiceType == TripServiceType.Charter)
        {
            if (request.ClientId is null)
                throw new InvalidOperationException("ClientId is required for Charter trips.");

            var client = await clientRepository.GetByIdAsync(request.ClientId.Value, cancellationToken)
                ?? throw new NotFoundException($"Client {request.ClientId} not found.");

            clientId = client.Id;
        }

        var stops = request.Stops.Select(s => (s.SequenceOrder, s.LocationName, s.Address));

        var trip = Trip.Create(
            request.ServiceType,
            clientId,
            request.VehicleId,
            request.PurchaseOrderNumber,
            request.VehicleType,
            DateTime.SpecifyKind(request.ScheduledAt, DateTimeKind.Utc),
            request.Notes,
            stops,
            request.SeatCapacity,
            request.PricePerSeat);

        await tripRepository.AddAsync(trip, cancellationToken);

        return new CreateTripResult(trip.Id);
    }
}
