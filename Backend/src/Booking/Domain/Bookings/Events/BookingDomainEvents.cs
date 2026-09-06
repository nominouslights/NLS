using NorthernLink.Shared.Kernel;

namespace NorthernLink.Booking.Domain.Bookings.Events;

/// <summary>Raised when a booking is created. Internal only — Booking publishes nothing yet.</summary>
public sealed record BookingCreatedDomainEvent(Guid BookingId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Raised when a booking's details are edited. Internal only.</summary>
public sealed record BookingUpdatedDomainEvent(Guid BookingId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Raised on Unconfirmed → Confirmed. Internal only (trip creation is a later batch).</summary>
public sealed record BookingConfirmedDomainEvent(Guid BookingId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Raised on cancellation. Internal only.</summary>
public sealed record BookingCancelledDomainEvent(Guid BookingId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
