using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class SubmitPostReportCommandHandler(
    ITripRepository tripRepository,
    IClientTripNotifier notifier)
    : IRequestHandler<SubmitPostReportCommand>
{
    public async Task Handle(SubmitPostReportCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        trip.SubmitPostReport(
            request.OdometerEnd,
            request.FuelAddedLitres,
            request.FuelCostDollars,
            request.HasIncident,
            request.IncidentType,
            request.IncidentDescription,
            request.AdditionalNotes,
            request.IsReadyToInvoice);

        await tripRepository.UpdateAsync(trip, cancellationToken);

        // Trip is now Completed -> notify the client's departures & arrivals list.
        await notifier.NotifyDepartureArrivalAsync(
            trip,
            ClientEmailTemplateType.ArrivalNotification,
            status: "Arrived",
            cancellationToken: cancellationToken);
    }
}
