using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Routes.Events;

/// <summary>Raised when a route is edited. Journaled and mapped to <c>RouteChangedIntegrationEvent</c> — see <see cref="RouteCreatedDomainEvent"/>.</summary>
public sealed record RouteUpdatedDomainEvent(Guid RouteId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
