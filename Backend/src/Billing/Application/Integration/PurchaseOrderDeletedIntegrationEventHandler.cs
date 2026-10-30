using Microsoft.Extensions.Logging;
using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Clients;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Billing.Application.Integration;

/// <summary>
/// Drops the <c>purchase_order_snapshots</c> row for a hard-deleted PO
/// (<c>clients.purchase-order-deleted</c>). POs carry no lifecycle and are genuinely
/// removable, so an upsert-only replica would keep pricing work against terms the client
/// withdrew. Deleting an absent row is a no-op, so redelivery is harmless — idempotent by
/// construction. Same tenancy shape as the changed handler: the tenant comes from the payload
/// and <see cref="AmbientTenant.Push"/> happens before the lookup, which also filters tenant
/// explicitly.
/// </summary>
public sealed class PurchaseOrderDeletedIntegrationEventHandler(
    IPurchaseOrderSnapshotRepository repository,
    ILogger<PurchaseOrderDeletedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<PurchaseOrderDeletedIntegrationEvent>
{
    public async Task Handle(
        PurchaseOrderDeletedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        using var tenantScope = AmbientTenant.Push(integrationEvent.TenantId);

        var snapshot = await repository.GetByIdAsync(
            integrationEvent.TenantId, integrationEvent.PurchaseOrderId, cancellationToken);

        if (snapshot is null)
        {
            logger.LogInformation(
                "Billing had no purchase order snapshot {PurchaseOrderId} to drop ({EventId}) — already gone",
                integrationEvent.PurchaseOrderId,
                integrationEvent.EventId);
            return;
        }

        repository.Remove(snapshot);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Billing dropped purchase order snapshot {PurchaseOrderId} ({EventId}): PO {PoNumber}",
            integrationEvent.PurchaseOrderId,
            integrationEvent.EventId,
            snapshot.PoNumber);
    }
}
