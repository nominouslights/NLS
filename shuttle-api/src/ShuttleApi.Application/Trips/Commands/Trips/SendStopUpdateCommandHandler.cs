using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class SendStopUpdateCommandHandler(
    ITripRepository tripRepository,
    IClientTripNotifier notifier)
    : IRequestHandler<SendStopUpdateCommand>
{
    public async Task Handle(SendStopUpdateCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        var stopLocation = request.StopId is null
            ? null
            : trip.Stops.FirstOrDefault(s => s.Id == request.StopId.Value)?.LocationName;

        var status = string.IsNullOrWhiteSpace(request.Status) ? "On Time" : request.Status!;

        await notifier.NotifyDepartureArrivalAsync(
            trip,
            ClientEmailTemplateType.StopUpdate,
            status,
            stopLocation,
            cancellationToken);
    }
}
