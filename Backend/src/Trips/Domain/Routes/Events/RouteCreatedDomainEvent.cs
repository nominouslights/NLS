using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Routes.Events;

/// <summary>
/// Raised when a route is created. Journals the aggregate so <c>RouteProjection</c>
/// populates <c>rm_routes</c>, and maps to <c>RouteChangedIntegrationEvent</c> in
/// <c>TripsIntegrationEventMapper</c> so the Booking module's corridor replica stays current.
/// </summary>
public sealed record RouteCreatedDomainEvent(Guid RouteId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
