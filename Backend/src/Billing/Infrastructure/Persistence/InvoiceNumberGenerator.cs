using Microsoft.EntityFrameworkCore;
using NorthernLink.Billing.Application.Abstractions;

namespace NorthernLink.Billing.Infrastructure.Persistence;

/// <summary>
/// Per-tenant "INV-0001, INV-0002, …" sequencing: count the tenant's existing invoices
/// inside the unit of work and add one — the Fleet retirement-certificate precedent.
/// Invoices are never hard-deleted (Void keeps the row), so the count is monotonic; the
/// unique index on (tenant_id, invoice_number) is the authoritative guard against a
/// concurrent duplicate. The count bypasses the tenant query filter and matches the
/// explicit tenant id so the sequence stays correct wherever it is called from.
/// </summary>
internal sealed class InvoiceNumberGenerator(BillingDbContext context) : IInvoiceNumberGenerator
{
    public async Task<string> NextInvoiceNumberAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var issued = await context.Invoices
            .IgnoreQueryFilters()
            .CountAsync(i => i.TenantId == tenantId, cancellationToken);

        return $"INV-{issued + 1:D4}";
    }
}
