using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Queries.OdometerHistory;

internal sealed class GetVehicleOdometerHistoryQueryHandler(
    IVehicleRepository vehicleRepository,
    ITripRepository tripRepository)
    : IRequestHandler<GetVehicleOdometerHistoryQuery, IReadOnlyList<OdometerHistoryEntryResult>>
{
    public async Task<IReadOnlyList<OdometerHistoryEntryResult>> Handle(
        GetVehicleOdometerHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdWithRecordsAsync(request.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle {request.VehicleId} not found.");

        var trips = await tripRepository.GetAllAsync(vehicleId: request.VehicleId, cancellationToken: cancellationToken);

        var entries = new List<OdometerHistoryEntryResult>();

        // Service record readings
        foreach (var record in vehicle.ServiceRecords.Where(r => r.OdometerAtService.HasValue))
        {
            var date = record.CompletedDate ?? record.StartedDate ?? record.ScheduledDate ?? record.CreatedAt;
            entries.Add(new OdometerHistoryEntryResult(
                date,
                record.OdometerAtService!.Value,
                "Service",
                record.Id,
                record.Title));
        }

        // Trip post-report readings (start and end)
        foreach (var trip in trips.Where(t => t.PostReport is not null))
        {
            entries.Add(new OdometerHistoryEntryResult(
                trip.PostReport!.SubmittedAt,
                trip.PostReport.OdometerEnd,
                "Trip",
                trip.Id,
                $"Trip completed — {trip.PostReport.DistanceKm} km"));
        }

        // Fuel entry odometer readings
        foreach (var entry in vehicle.FuelEntries.Where(e => e.OdometerAtFuelling.HasValue))
        {
            entries.Add(new OdometerHistoryEntryResult(
                entry.FuelledAt,
                entry.OdometerAtFuelling!.Value,
                "Fuel",
                entry.Id,
                $"{entry.FuelLitres:F1} L — ${entry.TotalCostDollars:F2}"));
        }

        return entries
            .OrderByDescending(e => e.Date)
            .ToList();
    }
}
