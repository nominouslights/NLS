using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Community.Queries;

internal sealed class GetBookingByReferenceQueryHandler(ITripRepository tripRepository)
    : IRequestHandler<GetBookingByReferenceQuery, BookingDetailResult>
{
    public async Task<BookingDetailResult> Handle(
        GetBookingByReferenceQuery request, CancellationToken cancellationToken)
    {
        var passenger = await tripRepository.GetPassengerByReferenceAsync(
            request.Reference, cancellationToken)
            ?? throw new KeyNotFoundException($"Booking reference '{request.Reference}' not found.");

        var trip = await tripRepository.GetByIdAsync(passenger.TripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {passenger.TripId} not found for booking {request.Reference}.");

        var route = passenger.Direction?.Equals("Outbound", StringComparison.OrdinalIgnoreCase) == true
            ? "Thompson → Lynn Lake"
            : "Lynn Lake → Thompson";

        var fare = passenger.Fare ?? 90m;
        var tripType = fare > 90m ? "Return" : "OneWay";

        return new BookingDetailResult(
            BookingReference: passenger.BookingReference!,
            FullName: passenger.Name,
            Phone: passenger.Phone,
            Email: passenger.Email,
            Direction: passenger.Direction,
            TripType: tripType,
            DepartureDate: DateOnly.FromDateTime(trip.ScheduledAt),
            Route: route,
            Fare: fare,
            Status: passenger.PaymentStatus.ToString(),
            CutoffDeadline: passenger.CutoffDeadline,
            BookedAt: passenger.BookedAt);
    }
}
