using NorthernLink.Shared.Messaging;

namespace NorthernLink.Booking.Application.Calendar.GetMonth;

/// <summary>
/// Per-date summaries for one corridor's month. Only dates with any booking activity
/// (a materialized BookingDay row and/or bookings) get an entry — dates absent from the
/// response have no bookings and render neutral.
/// </summary>
public sealed record GetBookingCalendarMonthQuery(
    Guid TenantId,
    Guid CorridorId,
    int Year,
    int Month) : IQuery<IReadOnlyList<CalendarDaySummaryResponse>>;

/// <summary>
/// One calendar cell. Sold/Pending/Capacity/Remaining/PassengerMinimum/NeededToConfirm are
/// the derived seat math (see SeatMath); BookingCount counts non-cancelled bookings;
/// HasOverrides flags a day whose minimum/capacity deviate from corridor/policy defaults;
/// BookingDayId/TripId are null until the day materializes (TripId always null this batch).
/// </summary>
public sealed record CalendarDaySummaryResponse(
    DateOnly Date,
    Guid? BookingDayId,
    Guid? TripId,
    int BookingCount,
    int Sold,
    int Pending,
    int Capacity,
    int Remaining,
    int PassengerMinimum,
    int NeededToConfirm,
    bool HasOverrides);
