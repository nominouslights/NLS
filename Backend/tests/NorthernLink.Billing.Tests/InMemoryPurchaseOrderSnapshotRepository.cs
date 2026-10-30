using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.PurchaseOrders;

namespace NorthernLink.Billing.Tests;

/// <summary>In-memory fake for consumer/handler tests — no EF, no Postgres.</summary>
public sealed class InMemoryPurchaseOrderSnapshotRepository : IPurchaseOrderSnapshotRepository
{
    public List<PurchaseOrderSnapshot> Snapshots { get; } = [];

    public int SaveCount { get; private set; }

    public Task<PurchaseOrderSnapshot?> GetByIdAsync(
        Guid tenantId, Guid purchaseOrderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Snapshots.FirstOrDefault(p => p.TenantId == tenantId && p.Id == purchaseOrderId));

    public Task<IReadOnlyList<PurchaseOrderSnapshot>> GetForClientAsync(
        Guid clientId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PurchaseOrderSnapshot>>(
            Snapshots.Where(p => p.ClientId == clientId).ToList());

    public void Add(PurchaseOrderSnapshot snapshot) => Snapshots.Add(snapshot);

    public void Remove(PurchaseOrderSnapshot snapshot) => Snapshots.Remove(snapshot);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
