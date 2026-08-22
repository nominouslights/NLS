using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Riders.Events;

/// <summary>
/// Raised when a passenger first enters the rider directory (their first manifest
/// appearance for a service type). Journaled; drives the rm_riders projection.
/// </summary>
public sealed record RiderCreatedDomainEvent(Guid RiderId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
