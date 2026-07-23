using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when a trip reaches Completed — whether by an explicit status change or by a
/// completed manifest attaching to it. The publish hook for the
/// <c>trips.trip-completed</c> integration event Billing consumes to record a billable trip.
/// </summary>
public sealed record TripCompletedDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
