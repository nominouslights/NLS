using Microsoft.EntityFrameworkCore;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Infrastructure.Persistence.Repositories;

internal sealed class PurchaseOrderRepository(AppDbContext dbContext) : IPurchaseOrderRepository
{
    public async Task<IReadOnlyList<PurchaseOrder>> GetByClientIdAsync(
        Guid clientId,
        CancellationToken cancellationToken = default) =>
        await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.LineItems)
            .Where(p => p.ClientId == clientId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);

    public async Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.PurchaseOrders
            .Include(p => p.LineItems)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<bool> ExistsByClientAndPoNumberAsync(
        Guid clientId,
        string poNumber,
        Guid? excludePurchaseOrderId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = poNumber.Trim();
        var query = dbContext.PurchaseOrders
            .Where(p => p.ClientId == clientId && p.PoNumber == normalized);

        if (excludePurchaseOrderId is not null)
            query = query.Where(p => p.Id != excludePurchaseOrderId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(
        PurchaseOrder purchaseOrder,
        IReadOnlyList<Guid> contractIds,
        CancellationToken cancellationToken = default)
    {
        await dbContext.PurchaseOrders.AddAsync(purchaseOrder, cancellationToken);
        await ReplaceContractLinksAsync(purchaseOrder.Id, contractIds, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        PurchaseOrder purchaseOrder,
        IReadOnlyList<Guid> contractIds,
        CancellationToken cancellationToken = default)
    {
        dbContext.PurchaseOrders.Update(purchaseOrder);
        await ReplaceContractLinksAsync(purchaseOrder.Id, contractIds, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetLinkedContractIdsAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default) =>
        await dbContext.ContractPurchaseOrders
            .AsNoTracking()
            .Where(x => x.PurchaseOrderId == purchaseOrderId)
            .Select(x => x.ContractId)
            .ToListAsync(cancellationToken);

    private async Task ReplaceContractLinksAsync(
        Guid purchaseOrderId,
        IReadOnlyList<Guid> contractIds,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.ContractPurchaseOrders
            .Where(x => x.PurchaseOrderId == purchaseOrderId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
            dbContext.ContractPurchaseOrders.RemoveRange(existing);

        foreach (var contractId in contractIds.Distinct())
        {
            await dbContext.ContractPurchaseOrders.AddAsync(
                ContractPurchaseOrder.Create(contractId, purchaseOrderId),
                cancellationToken);
        }
    }
}
