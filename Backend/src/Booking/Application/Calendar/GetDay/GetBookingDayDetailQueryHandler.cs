using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Domain.Bookings;

namespace NorthernLink.Booking.Application.Calendar.GetDay;

public sealed class GetBookingDayDetailQueryHandler(
    ICorridorLookupRepository corridors,
    IBookingDayRepository bookingDays,
    IBookingReadService bookingReads,
    ICorridorSettingsRepository corridorSettings,
    IBookingPolicyRepository policies)
    : IQueryHandler<GetBookingDayDetailQuery, BookingDayDetailResponse>
{
    public async Task<Result<BookingDayDetailResponse>> Handle(
        GetBookingDayDetailQuery query, CancellationToken cancellationToken)
    {
        var corridor = await corridors.GetAsync(query.CorridorId, cancellationToken);
        if (corridor is null)
        {
            return Result.Failure<BookingDayDetailResponse>(BookingErrors.CorridorNotFound);
        }

        var day = await bookingDays.GetAsync(query.CorridorId, query.Date, cancellationToken);
        var bookings = await bookingReads.GetForDateAsync(query.CorridorId, query.Date, cancellationToken);
        var settings = await corridorSettings.GetByCorridorAsync(query.CorridorId, cancellationToken);
        var policy = await policies.GetAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var capacity = SeatMath.ResolveCapacity(day?.SeatCapacityOverride, settings?.SeatCapacity, policy);
        var minimum = SeatMath.ResolveMinimum(day?.PassengerMinimumOverride, settings?.PassengerMinimum, policy);
        var seats = SeatMath.Compute(
            [.. bookings.Select(b => new BookingSeatRow(b.ServiceDate, b.Status, b.HoldExpiresAtUtc, b.Passengers.Count))],
            now,
            capacity,
            minimum);

        return Result.Success(new BookingDayDetailResponse(
            Date: query.Date,
            CorridorId: query.CorridorId,
            CorridorName: corridor.Name,
            BookingDayId: day?.Id,
            TripId: day?.TripId,
            PassengerMinimumOverride: day?.PassengerMinimumOverride,
            SeatCapacityOverride: day?.SeatCapacityOverride,
            Sold: seats.Sold,
            Pending: seats.Pending,
            Capacity: seats.Capacity,
            Remaining: seats.Remaining,
            PassengerMinimum: seats.PassengerMinimum,
            NeededToConfirm: seats.NeededToConfirm,
            Bookings: bookings));
    }
}
