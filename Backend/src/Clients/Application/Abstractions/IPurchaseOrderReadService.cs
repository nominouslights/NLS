using NorthernLink.Clients.Application.PurchaseOrders;

namespace NorthernLink.Clients.Application.Abstractions;

/// <summary>Read side for purchase order queries — returns response DTOs directly (tenant-scoped).</summary>
public interface IPurchaseOrderReadService
{
    Task<IReadOnlyList<PurchaseOrderResponse>> GetForClientAsync(Guid clientId, CancellationToken cancellationToken = default);
}
