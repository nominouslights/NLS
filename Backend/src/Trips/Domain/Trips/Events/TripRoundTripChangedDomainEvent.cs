using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when a trip's round-trip pairing changes after creation — a dispatcher merged
/// it into a round trip (or created a deadhead return for it), or unpaired it again.
/// Public: maps to <c>TripRoundTripChangedIntegrationEvent</c> so Billing can re-key its
/// not-yet-invoiced <c>billable_trips</c> replica rows.
/// </summary>
public sealed record TripRoundTripChangedDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
