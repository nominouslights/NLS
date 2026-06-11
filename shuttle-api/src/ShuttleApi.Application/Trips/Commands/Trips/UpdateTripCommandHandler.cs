using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class UpdateTripCommandHandler(
    ITripRepository tripRepository,
    IPurchaseOrderRepository purchaseOrderRepository)
    : IRequestHandler<UpdateTripCommand>
{
    public async Task Handle(UpdateTripCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        var (purchaseOrderId, purchaseOrderNumber) = await TripPurchaseOrderResolver.ResolveAsync(
            trip.ServiceType,
            trip.ClientId,
            request.PurchaseOrderId,
            request.PurchaseOrderNumber,
            purchaseOrderRepository,
            cancellationToken);

        var stops = request.Stops.Select(s => (s.SequenceOrder, s.LocationName, s.Address));

        trip.Update(
            request.VehicleId,
            purchaseOrderId,
            purchaseOrderNumber,
            request.VehicleType,
            DateTime.SpecifyKind(request.ScheduledAt, DateTimeKind.Utc),
            request.Notes,
            stops,
            request.SeatCapacity,
            request.PricePerSeat);

        await tripRepository.UpdateAsync(trip, cancellationToken);
    }
}
