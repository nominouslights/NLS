using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Clients;

/// <summary>
/// Published when a client purchase order is hard-deleted — routing key
/// <c>clients.purchase-order-deleted</c>. The counterpart to
/// <see cref="PurchaseOrderChangedIntegrationEvent"/>: POs carry no lifecycle and are
/// genuinely removable, so a replica that can only ever be upserted would keep pricing work
/// against terms the client withdrew. Id plus tenant is all a consumer needs to drop its
/// row; the source row is already gone by the time the handler runs. Delivery is
/// at-least-once and deleting an absent row is a no-op, so the handler is idempotent by
/// construction.
/// </summary>
public sealed record PurchaseOrderDeletedIntegrationEvent(
    Guid PurchaseOrderId,
    Guid TenantId) : IntegrationEvent;
