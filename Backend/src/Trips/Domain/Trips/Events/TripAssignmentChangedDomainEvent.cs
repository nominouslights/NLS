using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>Raised when a trip's driver or vehicle assignment changes. Internal only — see <see cref="TripScheduledDomainEvent"/>.</summary>
public sealed record TripAssignmentChangedDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
