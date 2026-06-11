using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;

namespace ShuttleApi.Application.Clients;

internal sealed class GetPurchaseOrderByIdQueryHandler(IPurchaseOrderRepository purchaseOrderRepository)
    : IRequestHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDetailResult>
{
    public async Task<PurchaseOrderDetailResult> Handle(
        GetPurchaseOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var purchaseOrder = await purchaseOrderRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Purchase order {request.Id} not found.");

        if (purchaseOrder.ClientId != request.ClientId)
            throw new NotFoundException($"Purchase order {request.Id} not found.");

        var linkedContractIds = await purchaseOrderRepository.GetLinkedContractIdsAsync(
            purchaseOrder.Id,
            cancellationToken);

        return new PurchaseOrderDetailResult(
            purchaseOrder.Id,
            purchaseOrder.ClientId,
            purchaseOrder.PoNumber,
            purchaseOrder.StartDate,
            purchaseOrder.Details,
            purchaseOrder.TotalValue,
            purchaseOrder.LineItems
                .OrderBy(i => i.SortOrder)
                .Select(i => new PurchaseOrderLineItemResult(
                    i.Id,
                    i.Description,
                    i.UnitRate,
                    i.Quantity,
                    i.LineTotal,
                    i.SortOrder))
                .ToList(),
            linkedContractIds);
    }
}
