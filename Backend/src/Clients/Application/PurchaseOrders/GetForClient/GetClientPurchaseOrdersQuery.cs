using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.PurchaseOrders.GetForClient;

/// <summary>Every purchase order of one client, most recently issued first.</summary>
public sealed record GetClientPurchaseOrdersQuery(Guid TenantId, Guid ClientId)
    : IQuery<IReadOnlyList<PurchaseOrderResponse>>;
