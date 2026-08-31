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
