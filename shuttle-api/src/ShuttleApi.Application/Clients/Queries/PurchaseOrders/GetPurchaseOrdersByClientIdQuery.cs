using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Clients;

public sealed record GetPurchaseOrdersByClientIdQuery(Guid ClientId)
    : IQuery<IReadOnlyList<PurchaseOrderSummaryResult>>;
