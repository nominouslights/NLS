using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised on a status transition that isn't Completed (Start, Cancel — Complete has its
/// own <see cref="TripCompletedDomainEvent"/>, Billing's feed). Internal only — see
/// <see cref="TripScheduledDomainEvent"/>.
/// </summary>
public sealed record TripStatusChangedDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
