using NorthernLink.Shared.Kernel;

namespace NorthernLink.Booking.Domain.BookingDays.Events;

/// <summary>Raised when a booking day materializes (first booking, or an admin override). Internal only.</summary>
public sealed record BookingDayCreatedDomainEvent(Guid BookingDayId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Raised when a day's minimum/capacity overrides change. Internal only.</summary>
public sealed record BookingDayOverridesChangedDomainEvent(Guid BookingDayId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Raised when the day confirms (threshold crossed up, or a Gift-a-Seat guarantee
/// re-confirming a Reverted day). Carries the caller-computed snapshot the aggregate itself
/// does not hold (corridor names, derived seat math) because the mapper turns this into the
/// public <c>booking.booking-day-confirmed</c> chain-reaction event, whose consumer needs
/// them. Mapped — this is Booking's first public contract.
/// </summary>
public sealed record BookingDayConfirmedDomainEvent(
    Guid BookingDayId,
    Guid TenantId,
    Guid CorridorId,
    string CorridorName,
    string Origin,
    string Destination,
    DateOnly ServiceDate,
    int SeatsSold,
    int SeatsMinimum,
    int SeatCapacity) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Raised when a Confirmed day drops below its minimum outside the cancellation window and
/// reverts. Carries the recipient snapshot (customers holding non-cancelled bookings that
/// day, no-email customers excluded) because the mapper publishes it to Notifications, which
/// never queries other modules. Mapped to <c>booking.booking-day-reverted</c>.
/// </summary>
public sealed record BookingDayRevertedDomainEvent(
    Guid BookingDayId,
    Guid TenantId,
    string CorridorName,
    DateOnly ServiceDate,
    int SeatsSold,
    int SeatsNeeded,
    IReadOnlyList<DayRevertRecipient> Recipients,
    Guid? TripId,
    string? TripNumber) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>One customer to notify that their trip is at risk (domain-side snapshot shape).</summary>
public sealed record DayRevertRecipient(string Name, string Email);

/// <summary>Raised when a dispatcher guarantees the day's minimum (Gift-a-Seat). Internal only.</summary>
public sealed record BookingDayGuaranteeSetDomainEvent(Guid BookingDayId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Raised when the Trips backlink (trip id + number) lands on the day. Internal only.</summary>
public sealed record BookingDayTripLinkedDomainEvent(Guid BookingDayId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
