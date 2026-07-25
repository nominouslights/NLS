using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.PurchaseOrders;

namespace NorthernLink.Clients.Tests;

/// <summary>In-memory fake of the write-side repository for handler tests.</summary>
internal sealed class InMemoryPurchaseOrderRepository : IPurchaseOrderRepository
{
    public List<PurchaseOrder> PurchaseOrders { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(PurchaseOrders.FirstOrDefault(p => p.Id == id));

    public void Add(PurchaseOrder purchaseOrder) => PurchaseOrders.Add(purchaseOrder);

    public void Remove(PurchaseOrder purchaseOrder) => PurchaseOrders.Remove(purchaseOrder);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
