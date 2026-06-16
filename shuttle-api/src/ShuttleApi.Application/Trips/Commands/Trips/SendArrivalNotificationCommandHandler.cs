using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class SendArrivalNotificationCommandHandler(
    ITripRepository tripRepository,
    IClientTripNotifier notifier)
    : IRequestHandler<SendArrivalNotificationCommand>
{
    public async Task Handle(SendArrivalNotificationCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        Guard.Against(trip.Status != TripStatus.Completed, "Trip must be completed to send an arrival notification.");

        await notifier.NotifyDepartureArrivalAsync(
            trip,
            ClientEmailTemplateType.ArrivalNotification,
            "Arrived",
            cancellationToken: cancellationToken);
    }
}
