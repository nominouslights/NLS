using Microsoft.Extensions.Logging;
using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.PurchaseOrders;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Clients;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Billing.Application.Integration;

/// <summary>
/// Maintains the <c>purchase_order_snapshots</c> replica: every
/// <c>clients.purchase-order-changed</c> event upserts the full billing-relevant snapshot
/// keyed on PurchaseOrderId, so replaying the same event (delivery is at-least-once)
/// converges on the same row — idempotent by construction. Runs outside any HTTP request, so
/// the tenant comes from the event payload; <see cref="AmbientTenant.Push"/> makes the RLS
/// session variable follow it on the live server (the lookup still filters tenant explicitly,
/// Fleet-consumer style, because the context's query-filter tenant was captured before the
/// push).
/// </summary>
public sealed class PurchaseOrderChangedIntegrationEventHandler(
    IPurchaseOrderSnapshotRepository repository,
    ILogger<PurchaseOrderChangedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<PurchaseOrderChangedIntegrationEvent>
{
    public async Task Handle(
        PurchaseOrderChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        using var tenantScope = AmbientTenant.Push(integrationEvent.TenantId);

        var snapshot = await repository.GetByIdAsync(
            integrationEvent.TenantId, integrationEvent.PurchaseOrderId, cancellationToken);

        var isNew = snapshot is null;
        if (snapshot is null)
        {
            snapshot = new PurchaseOrderSnapshot
            {
                Id = integrationEvent.PurchaseOrderId,
                TenantId = integrationEvent.TenantId,
            };
            repository.Add(snapshot);
        }

        snapshot.ClientId = integrationEvent.ClientId;
        snapshot.PoNumber = integrationEvent.PoNumber;
        snapshot.Issued = integrationEvent.Issued;
        snapshot.Expiry = integrationEvent.Expiry;
        snapshot.AmountCad = integrationEvent.AmountCad;
        snapshot.RoundTripRateCad = integrationEvent.RoundTripRateCad;
        snapshot.OneWayRateCad = integrationEvent.OneWayRateCad;
        snapshot.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Billing {Action} purchase order snapshot {PurchaseOrderId} ({EventId}): PO {PoNumber}, "
            + "round trip {RoundTripRate}, one way {OneWayRate}",
            isNew ? "inserted" : "updated",
            integrationEvent.PurchaseOrderId,
            integrationEvent.EventId,
            integrationEvent.PoNumber,
            integrationEvent.RoundTripRateCad,
            integrationEvent.OneWayRateCad);
    }
}
