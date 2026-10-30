using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when a Scheduled trip is moved onto a different catalogue route
/// (<see cref="Trip.ChangeRoute"/>). Carries the before/after route so the activity timeline
/// can say what changed. Internal only — no other module tracks a trip's route before it runs.
/// </summary>
public sealed record TripRouteChangedDomainEvent(
    Guid TripId,
    Guid? PreviousRouteId,
    string PreviousRouteName,
    Guid NewRouteId,
    string NewRouteName) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
