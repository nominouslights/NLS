using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class RemoveCargoItemCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<RemoveCargoItemCommand>
{
    public async Task Handle(RemoveCargoItemCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        trip.RemoveCargoItem(request.CargoItemId);

        await tripRepository.UpdateAsync(trip, cancellationToken);
    }
}
