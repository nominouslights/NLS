using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class GetTripByIdQueryHandler(ITripRepository tripRepository)
    : IRequestHandler<GetTripByIdQuery, TripDetailResult>
{
    public async Task<TripDetailResult> Handle(GetTripByIdQuery request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.Id} not found.");

        var stops = trip.Stops
            .OrderBy(s => s.SequenceOrder)
            .Select(s => new TripStopResult(s.Id, s.SequenceOrder, s.LocationName, s.Address))
            .ToList();

        TripPreInspectionResult? preInspection = null;
        if (trip.PreInspection is not null)
        {
            var items = trip.PreInspection.Items
                .Select(i => new TripInspectionItemResult(i.Id, i.ItemName, i.Passed, i.Notes))
                .ToList();

            preInspection = new TripPreInspectionResult(
                trip.PreInspection.Id,
                trip.PreInspection.OdometerStart,
                trip.PreInspection.SubmittedAt,
                items);
        }

        TripPostReportResult? postReport = null;
        if (trip.PostReport is not null)
        {
            postReport = new TripPostReportResult(
                trip.PostReport.Id,
                trip.PostReport.OdometerStart,
                trip.PostReport.OdometerEnd,
                trip.PostReport.DistanceKm,
                trip.PostReport.FuelAddedLitres,
                trip.PostReport.FuelCostDollars,
                trip.PostReport.HasIncident,
                trip.PostReport.IncidentType?.ToString(),
                trip.PostReport.IncidentDescription,
                trip.PostReport.AdditionalNotes,
                trip.PostReport.SubmittedAt,
                trip.PostReport.IsReadyToInvoice);
        }

        return new TripDetailResult(
            trip.Id,
            trip.ClientId,
            trip.VehicleId,
            trip.DriverId,
            trip.PurchaseOrderNumber,
            trip.VehicleType,
            trip.ScheduledAt,
            trip.Status.ToString(),
            trip.Notes,
            trip.CreatedAt,
            stops,
            preInspection,
            postReport);
    }
}
