using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Trips;

internal sealed class SubmitPostReportCommandHandler(
    ITripRepository tripRepository,
    IVehicleRepository vehicleRepository,
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
            request.IsReadyToInvoice,
            request.ExteriorNoNewDamage,
            request.InteriorCleanedAndChecked,
            request.PassengersDisembarkedSafely,
            request.AllCargoDeliveredAndAccounted,
            request.VehicleSecuredAndPluggedIn,
            request.KeysReturnedAndSecured);

        await tripRepository.UpdateAsync(trip, cancellationToken);

        // Keep vehicle odometer in sync with the completed trip's end reading
        if (trip.VehicleId.HasValue)
        {
            var vehicle = await vehicleRepository.GetByIdAsync(trip.VehicleId.Value, cancellationToken);
            if (vehicle is not null && request.OdometerEnd > vehicle.CurrentOdometerKm)
            {
                vehicle.UpdateOdometer(request.OdometerEnd);
                await vehicleRepository.UpdateAsync(vehicle, cancellationToken);
            }
        }

        // Trip is now Completed -> notify the client's departures & arrivals list.
        await notifier.NotifyDepartureArrivalAsync(
            trip,
            ClientEmailTemplateType.ArrivalNotification,
            status: "Arrived",
            cancellationToken: cancellationToken);
    }
}
