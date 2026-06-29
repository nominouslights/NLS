using Microsoft.EntityFrameworkCore;
using ShuttleApi.Domain.Billing;

namespace ShuttleApi.Infrastructure.Persistence.Repositories;

internal sealed class InvoiceRepository(AppDbContext dbContext) : IInvoiceRepository
{
    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Invoice>> GetByClientIdAsync(
        Guid clientId,
        InvoiceStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Invoices
            .Include(i => i.LineItems)
            .Where(i => i.ClientId == clientId);

        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        return await query
            .OrderByDescending(i => i.IssuedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountForYearAsync(int year, CancellationToken cancellationToken = default) =>
        await dbContext.Invoices
            .CountAsync(i => i.CreatedAt.Year == year, cancellationToken);

    public async Task<HashSet<Guid>> GetInvoicedTripIdsAsync(CancellationToken cancellationToken = default)
    {
        var ids = await dbContext.InvoiceLineItems
            .Where(l => l.TripId.HasValue)
            .Select(l => l.TripId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        await dbContext.Invoices.AddAsync(invoice, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (dbContext.Entry(invoice).State == EntityState.Detached)
            dbContext.Invoices.Update(invoice);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
