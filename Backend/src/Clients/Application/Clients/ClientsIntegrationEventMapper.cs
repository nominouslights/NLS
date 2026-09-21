using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Clients;
using NorthernLink.Shared.Kernel;
using NorthernLink.Clients.Domain.Clients;
using NorthernLink.Clients.Domain.Clients.Events;
using NorthernLink.Clients.Domain.Contracts;
using NorthernLink.Clients.Domain.Contracts.Events;
using NorthernLink.Clients.Domain.PurchaseOrders;
using NorthernLink.Clients.Domain.PurchaseOrders.Events;

namespace NorthernLink.Clients.Application.Clients;

/// <summary>
/// Clients' explicit domain-event → integration-event translation. Client create/update
/// both map to <c>ClientChangedIntegrationEvent</c> and every contract lifecycle event maps
/// to the full-snapshot <c>ContractChangedIntegrationEvent</c>, so consumers (Trips'
/// <c>client_lookup</c>, Billing's <c>contract_snapshots</c>) maintain replicas by upsert.
/// Purchase orders now map too: since a PO carries its own negotiated pricing terms,
/// Billing needs a <c>purchase_order_snapshots</c> replica to price a trip from its PO
/// rather than only from the contract. PO create/update map to the full-snapshot
/// <c>PurchaseOrderChangedIntegrationEvent</c>; a hard delete maps to
/// <c>PurchaseOrderDeletedIntegrationEvent</c> so the replica can drop the row (POs, unlike
/// contracts, are genuinely removable).
/// </summary>
public sealed class ClientsIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate) =>
        domainEvent switch
        {
            ClientCreatedDomainEvent or ClientUpdatedDomainEvent when aggregate is Client client =>
                new ClientChangedIntegrationEvent(
                    client.Id,
                    client.TenantId,
                    client.Name,
                    client.Type.ToString(),
                    client.ServiceType?.ToString(),
                    client.Tag),
            ContractActivatedDomainEvent or ContractUpdatedDomainEvent or ContractTerminatedDomainEvent
                when aggregate is Contract contract =>
                new ContractChangedIntegrationEvent(
                    contract.Id,
                    contract.TenantId,
                    contract.ClientId,
                    contract.ClientName,
                    contract.StartDate,
                    contract.EndDate,
                    contract.BillingModel.ToString(),
                    contract.RatePerRoundTripCad,
                    contract.BudgetCode,
                    contract.BillingFrequency.ToString(),
                    contract.NetTermsDays,
                    contract.DefaultPoNumber,
                    contract.Status.ToString()),
            PurchaseOrderCreatedDomainEvent or PurchaseOrderUpdatedDomainEvent
                when aggregate is PurchaseOrder purchaseOrder =>
                new PurchaseOrderChangedIntegrationEvent(
                    purchaseOrder.Id,
                    purchaseOrder.TenantId,
                    purchaseOrder.ClientId,
                    purchaseOrder.PoNumber,
                    purchaseOrder.Issued,
                    purchaseOrder.Expiry,
                    purchaseOrder.AmountCad,
                    purchaseOrder.RoundTripRateCad,
                    purchaseOrder.OneWayRateCad),
            // Deleted carries id + tenant only: the row is gone, and a consumer only needs to
            // know which replica row to drop.
            PurchaseOrderDeletedDomainEvent deleted =>
                new PurchaseOrderDeletedIntegrationEvent(deleted.PurchaseOrderId, deleted.TenantId),
            _ => null,
        };
}
