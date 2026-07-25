using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>Raised when confirmed seat demand changes (the Manifests screen). Internal only — see <see cref="TripScheduledDomainEvent"/>.</summary>
public sealed record TripDemandRecordedDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
