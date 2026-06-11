using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Application.Clients;

internal sealed class GetPurchaseOrdersByClientIdQueryHandler(IPurchaseOrderRepository purchaseOrderRepository)
    : IRequestHandler<GetPurchaseOrdersByClientIdQuery, IReadOnlyList<PurchaseOrderSummaryResult>>
{
    public async Task<IReadOnlyList<PurchaseOrderSummaryResult>> Handle(
        GetPurchaseOrdersByClientIdQuery request,
        CancellationToken cancellationToken)
    {
        var purchaseOrders = await purchaseOrderRepository.GetByClientIdAsync(request.ClientId, cancellationToken);
        var results = new List<PurchaseOrderSummaryResult>(purchaseOrders.Count);

        foreach (var purchaseOrder in purchaseOrders)
        {
            var linkedContractIds = await purchaseOrderRepository.GetLinkedContractIdsAsync(
                purchaseOrder.Id,
                cancellationToken);

            results.Add(new PurchaseOrderSummaryResult(
                purchaseOrder.Id,
                purchaseOrder.ClientId,
                purchaseOrder.PoNumber,
                purchaseOrder.StartDate,
                purchaseOrder.Details,
                purchaseOrder.TotalValue,
                purchaseOrder.LineItems.Count,
                linkedContractIds));
        }

        return results;
    }
}
