using NorthernLink.Shared.Kernel;

namespace NorthernLink.Booking.Domain.Settings.Events;

/// <summary>Raised when the tenant's policy row materializes with defaults. Internal only.</summary>
public sealed record BookingPolicyCreatedDomainEvent(Guid PolicyId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Raised when the tenant's policy is edited. Internal only.</summary>
public sealed record BookingPolicyUpdatedDomainEvent(Guid PolicyId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Raised when a corridor's overrides are created or edited. Internal only.</summary>
public sealed record CorridorBookingSettingsChangedDomainEvent(Guid SettingsId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
