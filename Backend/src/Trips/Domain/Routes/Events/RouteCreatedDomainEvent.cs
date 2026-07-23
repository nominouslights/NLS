using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Routes.Events;

/// <summary>
/// Raised when a route is created. Internal only (maps to null in
/// <c>TripsIntegrationEventMapper</c>); its purpose is to give the aggregate a journal
/// row so <c>RouteProjection</c> populates <c>rm_routes</c>.
/// </summary>
public sealed record RouteCreatedDomainEvent(Guid RouteId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
