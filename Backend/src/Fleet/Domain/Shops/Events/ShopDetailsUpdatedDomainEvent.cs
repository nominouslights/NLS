using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Shops.Events;

/// <summary>
/// Raised when a shop's details are edited. Internal to the module:
/// <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record ShopDetailsUpdatedDomainEvent(Guid ShopId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
