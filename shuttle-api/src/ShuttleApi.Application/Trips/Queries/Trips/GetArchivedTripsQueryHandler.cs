using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class GetArchivedTripsQueryHandler(ITripRepository tripRepository)
    : IRequestHandler<GetArchivedTripsQuery, IReadOnlyList<ArchivedTripResult>>
{
    public async Task<IReadOnlyList<ArchivedTripResult>> Handle(
        GetArchivedTripsQuery request,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddYears(-1);
        await tripRepository.PurgeExpiredAsync(cutoff, cancellationToken);

        var archived = await tripRepository.GetAllArchivedAsync(cancellationToken);

        return archived.Select(t =>
        {
            var orderedStops = t.Stops.OrderBy(s => s.SequenceOrder).ToList();
            return new ArchivedTripResult(
                t.Id,
                t.ClientId,
                t.VehicleId,
                t.DriverId,
                t.ServiceType.ToString(),
                t.PurchaseOrderNumber,
                t.VehicleType,
                t.ScheduledAt,
                t.Status.ToString(),
                t.Notes,
                t.CreatedAt,
                t.Stops.Count,
                orderedStops.FirstOrDefault()?.LocationName,
                orderedStops.LastOrDefault()?.LocationName,
                t.SeatCapacity,
                t.Passengers.Count,
                t.DeletedAt);
        }).ToList();
    }
}
