using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal static class TripPurchaseOrderResolver
{
    public static async Task<(Guid? PurchaseOrderId, string? PurchaseOrderNumber)> ResolveAsync(
        TripServiceType serviceType,
        Guid? clientId,
        Guid? purchaseOrderId,
        string? purchaseOrderNumber,
        IPurchaseOrderRepository purchaseOrderRepository,
        CancellationToken cancellationToken)
    {
        if (serviceType == TripServiceType.Community)
        {
            if (purchaseOrderId is not null || !string.IsNullOrWhiteSpace(purchaseOrderNumber))
                throw new ArgumentException("Purchase orders are not supported for Community trips.");

            return (null, null);
        }

        if (purchaseOrderId is not null)
        {
            if (clientId is null)
                throw new ArgumentException("ClientId is required when selecting a purchase order.");

            var purchaseOrder = await purchaseOrderRepository.GetByIdAsync(purchaseOrderId.Value, cancellationToken)
                ?? throw new NotFoundException($"Purchase order {purchaseOrderId} not found.");

            if (purchaseOrder.ClientId != clientId.Value)
                throw new ArgumentException("Purchase order does not belong to the selected client.");

            return (purchaseOrder.Id, purchaseOrder.PoNumber);
        }

        return (null, string.IsNullOrWhiteSpace(purchaseOrderNumber) ? null : purchaseOrderNumber.Trim());
    }
}
