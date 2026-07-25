using NorthernLink.Clients.Domain.PurchaseOrders;

namespace NorthernLink.Clients.Application.Abstractions;

/// <summary>Write-side persistence for the PurchaseOrder aggregate (tenant-scoped).</summary>
public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(PurchaseOrder purchaseOrder);

    void Remove(PurchaseOrder purchaseOrder);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
