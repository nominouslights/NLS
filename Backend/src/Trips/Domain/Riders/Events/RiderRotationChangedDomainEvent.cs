using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Riders.Events;

/// <summary>
/// Raised when a contract-crew rider's rotation is set or cleared (a real change — setting
/// the same value again is a no-op). Journaled; drives the rm_riders projection.
/// </summary>
public sealed record RiderRotationChangedDomainEvent(Guid RiderId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
