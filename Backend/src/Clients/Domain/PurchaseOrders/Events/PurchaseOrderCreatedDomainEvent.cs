using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.PurchaseOrders.Events;

/// <summary>
/// Raised when a purchase order is recorded. Every aggregate write must raise an event —
/// the projection worker polls <c>event_journal</c>, and an eventless write produces no
/// journal row, leaving the read model silently stale. Internal to the module:
/// <c>ClientsIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record PurchaseOrderCreatedDomainEvent(Guid PurchaseOrderId, Guid ClientId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
