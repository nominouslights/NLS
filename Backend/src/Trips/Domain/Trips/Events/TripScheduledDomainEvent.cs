using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when a trip is scheduled — the wizard's ad-hoc path or the generation worker's
/// template-materialized path. Internal only (maps to null in
/// <c>TripsIntegrationEventMapper</c>); its purpose is to give the aggregate a journal row
/// so <c>TripProjection</c> populates <c>rm_trips</c> — projections drive off the journal,
/// not off "interesting to other modules".
/// </summary>
public sealed record TripScheduledDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
