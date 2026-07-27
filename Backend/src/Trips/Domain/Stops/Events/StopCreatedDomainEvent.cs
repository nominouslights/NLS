using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Stops.Events;

/// <summary>
/// Raised when a stop is created. Internal only (maps to null in
/// <c>TripsIntegrationEventMapper</c>); its purpose is to give the aggregate a journal
/// row so <c>StopProjection</c> populates <c>rm_stops</c>.
/// </summary>
public sealed record StopCreatedDomainEvent(Guid StopId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
