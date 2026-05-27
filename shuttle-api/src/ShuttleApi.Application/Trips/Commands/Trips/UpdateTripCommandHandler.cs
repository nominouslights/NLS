using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class UpdateTripCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<UpdateTripCommand>
{
    public async Task Handle(UpdateTripCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        var stops = request.Stops.Select(s => (s.SequenceOrder, s.LocationName, s.Address));

        trip.Update(
            request.PurchaseOrderNumber,
            request.VehicleType,
            DateTime.SpecifyKind(request.ScheduledAt, DateTimeKind.Utc),
            request.Notes,
            stops);

        await tripRepository.UpdateAsync(trip, cancellationToken);
    }
}
