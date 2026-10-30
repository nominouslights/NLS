using Microsoft.EntityFrameworkCore;
using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.PurchaseOrders;

namespace NorthernLink.Billing.Infrastructure.Persistence;

/// <summary>
/// The <c>purchase_order_snapshots</c> replica over <see cref="BillingDbContext"/>, modelled
/// on <see cref="ContractSnapshotRepository"/>. <see cref="GetByIdAsync"/> is called by the
/// integration-event consumer, which runs outside any HTTP request: the ambient tenant filter
/// would compare against null and match nothing, so it bypasses the filter and matches the
/// tenant id carried by the event instead (the Fleet inspection-consumer pattern).
/// <see cref="GetForClientAsync"/> runs on the request path and stays behind the tenant
/// filter — draft generation loads the client's POs once and matches numbers in memory, so
/// there is no per-PO-number query here.
/// </summary>
internal sealed class PurchaseOrderSnapshotRepository(BillingDbContext context) : IPurchaseOrderSnapshotRepository
{
    public Task<PurchaseOrderSnapshot?> GetByIdAsync(
        Guid tenantId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default) =>
        context.PurchaseOrderSnapshots
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == purchaseOrderId, cancellationToken);

    public async Task<IReadOnlyList<PurchaseOrderSnapshot>> GetForClientAsync(
        Guid clientId,
        CancellationToken cancellationToken = default) =>
        await context.PurchaseOrderSnapshots
            .Where(p => p.ClientId == clientId)
            .OrderByDescending(p => p.Issued)
            .ToListAsync(cancellationToken);

    public void Add(PurchaseOrderSnapshot snapshot) => context.PurchaseOrderSnapshots.Add(snapshot);

    public void Remove(PurchaseOrderSnapshot snapshot) => context.PurchaseOrderSnapshots.Remove(snapshot);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
