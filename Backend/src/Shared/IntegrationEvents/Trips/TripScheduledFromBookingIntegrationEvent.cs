using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Trips;

/// <summary>
/// Published when Trips materializes a community trip from a confirmed booking day —
/// routing key <c>trips.trip-scheduled-from-booking</c>. The backlink half of the
/// booking-day chain reaction: Booking consumes it (outbox polling — it stores a fact,
/// triggers nothing) to stamp <c>BookingDay.TripId</c>/<c>TripNumber</c> so the calendar
/// can link to the trip. Idempotent by construction: at most one trip ever exists per
/// (tenant, booking day), so replays carry the same TripId.
/// </summary>
public sealed record TripScheduledFromBookingIntegrationEvent(
    Guid TripId,
    string TripNumber,
    Guid TenantId,
    Guid BookingDayId,
    DateOnly ServiceDate) : IntegrationEvent;
