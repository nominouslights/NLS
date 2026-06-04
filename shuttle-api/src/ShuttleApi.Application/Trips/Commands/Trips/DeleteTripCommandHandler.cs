using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class DeleteTripCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<DeleteTripCommand>
{
    public async Task Handle(DeleteTripCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        trip.SoftDelete();

        await tripRepository.UpdateAsync(trip, cancellationToken);
    }
}
