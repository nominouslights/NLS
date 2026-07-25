using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;

namespace NorthernLink.Clients.Application.PurchaseOrders.GetForClient;

public sealed class GetClientPurchaseOrdersQueryHandler(IPurchaseOrderReadService readService)
    : IQueryHandler<GetClientPurchaseOrdersQuery, IReadOnlyList<PurchaseOrderResponse>>
{
    public async Task<Result<IReadOnlyList<PurchaseOrderResponse>>> Handle(
        GetClientPurchaseOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var purchaseOrders = await readService.GetForClientAsync(query.ClientId, cancellationToken);
        return Result.Success(purchaseOrders);
    }
}
