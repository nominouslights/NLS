using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class DispatchTripCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<DispatchTripCommand>
{
    public async Task Handle(DispatchTripCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        trip.Dispatch();

        await tripRepository.UpdateAsync(trip, cancellationToken);
    }
}
