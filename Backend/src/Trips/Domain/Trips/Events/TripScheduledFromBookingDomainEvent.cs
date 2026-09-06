using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when a community trip is materialized from a confirmed booking day
/// (<see cref="Trip.ScheduleFromBooking"/>) — instead of, not alongside,
/// <see cref="TripScheduledDomainEvent"/>: one creation, one journal row (either event
/// gives <c>TripProjection</c> the row it drives off — projections match on the aggregate
/// type). Mapped: <c>TripsIntegrationEventMapper</c> publishes
/// <c>trips.trip-scheduled-from-booking</c> so Booking can stamp the
/// <c>BookingDay.TripId</c>/<c>TripNumber</c> backlink.
/// </summary>
public sealed record TripScheduledFromBookingDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
