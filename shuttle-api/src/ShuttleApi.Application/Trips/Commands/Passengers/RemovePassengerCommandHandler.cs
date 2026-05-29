using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class RemovePassengerCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<RemovePassengerCommand>
{
    public async Task Handle(RemovePassengerCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        trip.RemovePassenger(request.PassengerId);

        await tripRepository.UpdateAsync(trip, cancellationToken);
    }
}
