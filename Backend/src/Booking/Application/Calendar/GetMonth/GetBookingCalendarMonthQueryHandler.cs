using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Domain.Bookings;

namespace NorthernLink.Booking.Application.Calendar.GetMonth;

public sealed class GetBookingCalendarMonthQueryHandler(
    ICorridorLookupRepository corridors,
    IBookingDayRepository bookingDays,
    IBookingReadService bookingReads,
    ICorridorSettingsRepository corridorSettings,
    IBookingPolicyRepository policies)
    : IQueryHandler<GetBookingCalendarMonthQuery, IReadOnlyList<CalendarDaySummaryResponse>>
{
    public async Task<Result<IReadOnlyList<CalendarDaySummaryResponse>>> Handle(
        GetBookingCalendarMonthQuery query, CancellationToken cancellationToken)
    {
        if (query.Month is < 1 or > 12 || query.Year is < 2000 or > 2100)
        {
            return Result.Failure<IReadOnlyList<CalendarDaySummaryResponse>>(CalendarErrors.InvalidMonth);
        }

        var corridor = await corridors.GetAsync(query.CorridorId, cancellationToken);
        if (corridor is null)
        {
            return Result.Failure<IReadOnlyList<CalendarDaySummaryResponse>>(BookingErrors.CorridorNotFound);
        }

        var from = new DateOnly(query.Year, query.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var days = await bookingDays.GetForRangeAsync(query.CorridorId, from, to, cancellationToken);
        var seatRows = await bookingReads.GetSeatRowsAsync(query.CorridorId, from, to, cancellationToken);
        var settings = await corridorSettings.GetByCorridorAsync(query.CorridorId, cancellationToken);
        var policy = await policies.GetAsync(cancellationToken);

        var daysByDate = days.ToDictionary(d => d.ServiceDate);
        var rowsByDate = seatRows.ToLookup(r => r.ServiceDate);
        var now = DateTimeOffset.UtcNow;

        var dates = daysByDate.Keys
            .Union(seatRows.Select(r => r.ServiceDate))
            .Order()
            .ToList();

        var summaries = new List<CalendarDaySummaryResponse>(dates.Count);
        foreach (var date in dates)
        {
            var day = daysByDate.GetValueOrDefault(date);
            var rows = rowsByDate[date].ToList();

            var capacity = SeatMath.ResolveCapacity(day?.SeatCapacityOverride, settings?.SeatCapacity, policy);
            var minimum = SeatMath.ResolveMinimum(day?.PassengerMinimumOverride, settings?.PassengerMinimum, policy);
            var seats = SeatMath.Compute(rows, now, capacity, minimum);

            summaries.Add(new CalendarDaySummaryResponse(
                Date: date,
                BookingDayId: day?.Id,
                TripId: day?.TripId,
                TripNumber: day?.TripNumber,
                Status: day?.Status ?? Domain.BookingDays.BookingDayStatus.Unconfirmed,
                MinimumGuaranteed: day?.MinimumGuaranteed ?? false,
                BookingCount: rows.Count(r => r.Status != BookingStatus.Cancelled),
                Sold: seats.Sold,
                Pending: seats.Pending,
                Capacity: seats.Capacity,
                Remaining: seats.Remaining,
                PassengerMinimum: seats.PassengerMinimum,
                NeededToConfirm: seats.NeededToConfirm,
                HasOverrides: day is { PassengerMinimumOverride: not null } or { SeatCapacityOverride: not null }));
        }

        return Result.Success<IReadOnlyList<CalendarDaySummaryResponse>>(summaries);
    }
}
