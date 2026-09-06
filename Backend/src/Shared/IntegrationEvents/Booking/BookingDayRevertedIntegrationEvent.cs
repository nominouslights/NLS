using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Booking;

/// <summary>
/// Published when a Confirmed booking day drops back below its passenger minimum outside the
/// cancellation window and reverts — routing key <c>booking.booking-day-reverted</c>. A
/// storing/consuming event delivered by outbox polling: Notifications consumes it to send the
/// "trip at risk" email. The event carries the full <see cref="Recipients"/> snapshot (name +
/// email of every customer holding a non-cancelled booking that day, customers without an
/// email excluded) because Notifications never queries other modules. <see cref="TripId"/> /
/// <see cref="TripNumber"/> are the day's trip backlink when it has already arrived (the trip
/// is created asynchronously, so early reverts may carry null). Consumers must be idempotent
/// on <see cref="IntegrationEvent.EventId"/>.
/// </summary>
public sealed record BookingDayRevertedIntegrationEvent(
    Guid BookingDayId,
    Guid TenantId,
    string CorridorName,
    DateOnly ServiceDate,
    int SeatsSold,
    int SeatsNeeded,
    IReadOnlyList<BookingDayRevertRecipient> Recipients,
    Guid? TripId,
    string? TripNumber) : IntegrationEvent;

/// <summary>One customer to notify that their trip is at risk.</summary>
public sealed record BookingDayRevertRecipient(string Name, string Email);
