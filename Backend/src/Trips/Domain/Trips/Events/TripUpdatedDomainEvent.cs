using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>Raised when a Scheduled trip's plan is edited. Internal only — see <see cref="TripScheduledDomainEvent"/>.</summary>
public sealed record TripUpdatedDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
