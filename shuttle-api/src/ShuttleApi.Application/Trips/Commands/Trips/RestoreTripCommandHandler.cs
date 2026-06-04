using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class RestoreTripCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<RestoreTripCommand>
{
    public async Task Handle(RestoreTripCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetDeletedByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Deleted trip {request.TripId} not found.");

        trip.Restore();

        await tripRepository.UpdateAsync(trip, cancellationToken);
    }
}
