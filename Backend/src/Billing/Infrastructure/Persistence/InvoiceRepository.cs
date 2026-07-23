using Microsoft.EntityFrameworkCore;
using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.Invoices;

namespace NorthernLink.Billing.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="BillingDbContext"/> (tenant-filtered).</summary>
internal sealed class InvoiceRepository(BillingDbContext context) : IInvoiceRepository
{
    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Invoices.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public void Add(Invoice invoice) => context.Invoices.Add(invoice);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
