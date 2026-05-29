using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class GetTripsQueryHandler(ITripRepository tripRepository)
    : IRequestHandler<GetTripsQuery, IReadOnlyList<TripListItemResult>>
{
    public async Task<IReadOnlyList<TripListItemResult>> Handle(GetTripsQuery request, CancellationToken cancellationToken)
    {
        var trips = await tripRepository.GetAllAsync(
            request.Status,
            request.ClientId,
            request.DriverId,
            request.VehicleId,
            request.ServiceType,
            cancellationToken);

        return trips.Select(t =>
        {
            var orderedStops = t.Stops.OrderBy(s => s.SequenceOrder).ToList();
            return new TripListItemResult(
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
                t.Passengers.Count);
        }).ToList();
    }
}
