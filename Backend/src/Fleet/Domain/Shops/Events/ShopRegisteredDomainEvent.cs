using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Shops.Events;

/// <summary>
/// Raised when a shop / parts partner is registered. Every aggregate write must raise an
/// event — an eventless write produces no journal row and the read model silently goes
/// stale. Internal to the module: <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record ShopRegisteredDomainEvent(Guid ShopId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
