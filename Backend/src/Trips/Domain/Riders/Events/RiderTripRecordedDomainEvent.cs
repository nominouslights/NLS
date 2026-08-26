using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Riders.Events;

/// <summary>
/// Raised when a manifest appearance actually changed the rider (new trip counted, or the
/// latest-trip fields advanced) — never on a converging redelivery. Journaled; drives the
/// rm_riders projection.
/// </summary>
public sealed record RiderTripRecordedDomainEvent(Guid RiderId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
