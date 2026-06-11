using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class UpdateTripStatusCommandHandler(
    ITripRepository tripRepository,
    IClientTripNotifier notifier)
    : IRequestHandler<UpdateTripStatusCommand>
{
    public async Task Handle(UpdateTripStatusCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        trip.UpdateStatus(request.Status);

        await tripRepository.UpdateAsync(trip, cancellationToken);

        if (request.Status == TripStatus.EnRoute)
        {
            await notifier.NotifyDepartureArrivalAsync(
                trip,
                ClientEmailTemplateType.DepartureNotification,
                status: "On Time",
                cancellationToken: cancellationToken);
        }
    }
}
