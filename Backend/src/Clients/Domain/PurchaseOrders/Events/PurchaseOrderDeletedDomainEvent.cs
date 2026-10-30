using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.PurchaseOrders.Events;

/// <summary>
/// Raised by <see cref="PurchaseOrder.MarkDeleted"/> just before the row is hard-deleted.
/// A delete raises nothing on its own — the audit pipeline only writes the synthetic
/// <c>aggregate-deleted</c> journal row, which never reaches the integration-event mapper.
/// This event is what lets <c>ClientsIntegrationEventMapper</c> emit
/// <c>PurchaseOrderDeletedIntegrationEvent</c> so Billing drops its replica row instead of
/// pricing against terms that no longer exist.
/// </summary>
public sealed record PurchaseOrderDeletedDomainEvent(Guid PurchaseOrderId, Guid ClientId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
