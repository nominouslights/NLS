using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class AddTripStopCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<AddTripStopCommand, TripStopResult>
{
    public async Task<TripStopResult> Handle(AddTripStopCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        var stop = trip.AddStop(request.InsertAtSequenceOrder, request.LocationName, request.Address);

        await tripRepository.UpdateAsync(trip, cancellationToken);

        return new TripStopResult(stop.Id, stop.SequenceOrder, stop.LocationName, stop.Address);
    }
}
