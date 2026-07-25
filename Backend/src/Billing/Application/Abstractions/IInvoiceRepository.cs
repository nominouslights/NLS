using NorthernLink.Billing.Domain.Invoices;

namespace NorthernLink.Billing.Application.Abstractions;

/// <summary>Write-side repository for the Invoice aggregate (tenant-filtered context).</summary>
public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Invoice invoice);

    /// <summary>One save commits the invoice and any billable-trip claim changes atomically —
    /// every Billing repository shares the module's scoped DbContext.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
