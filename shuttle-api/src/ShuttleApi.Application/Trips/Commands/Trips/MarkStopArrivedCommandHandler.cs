using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class MarkStopArrivedCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<MarkStopArrivedCommand>
{
    public async Task Handle(MarkStopArrivedCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        trip.MarkStopArrived(request.StopId);
        await tripRepository.UpdateAsync(trip, cancellationToken);
    }
}
