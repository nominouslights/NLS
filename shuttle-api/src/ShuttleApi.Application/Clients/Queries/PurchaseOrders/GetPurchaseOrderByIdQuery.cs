using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Clients;

public sealed record GetPurchaseOrderByIdQuery(Guid ClientId, Guid Id)
    : IQuery<PurchaseOrderDetailResult>;
