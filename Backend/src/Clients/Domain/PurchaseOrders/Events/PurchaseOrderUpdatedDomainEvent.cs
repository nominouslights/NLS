using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.PurchaseOrders.Events;

/// <summary>
/// Raised when a purchase order's details are edited. Internal to the module:
/// <c>ClientsIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record PurchaseOrderUpdatedDomainEvent(Guid PurchaseOrderId, Guid ClientId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
