using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class MarkStopDepartedCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<MarkStopDepartedCommand>
{
    public async Task Handle(MarkStopDepartedCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        trip.MarkStopDeparted(request.StopId);
        await tripRepository.UpdateAsync(trip, cancellationToken);
    }
}
