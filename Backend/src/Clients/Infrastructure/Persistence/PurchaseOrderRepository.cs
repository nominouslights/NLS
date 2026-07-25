using Microsoft.EntityFrameworkCore;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.PurchaseOrders;

namespace NorthernLink.Clients.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="ClientsDbContext"/> (tenant-filtered).</summary>
internal sealed class PurchaseOrderRepository(ClientsDbContext context) : IPurchaseOrderRepository
{
    public Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void Add(PurchaseOrder purchaseOrder) => context.PurchaseOrders.Add(purchaseOrder);

    public void Remove(PurchaseOrder purchaseOrder) => context.PurchaseOrders.Remove(purchaseOrder);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
