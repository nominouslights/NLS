namespace NorthernLink.Billing.Application.Abstractions;

/// <summary>
/// Issues the next "INV-…" number in the tenant's sequence. The number is provisional
/// until the invoice saves — the unique index on (tenant_id, invoice_number) is the
/// authoritative guard against a concurrent duplicate (the Fleet retirement-certificate
/// sequencing precedent).
/// </summary>
public interface IInvoiceNumberGenerator
{
    Task<string> NextInvoiceNumberAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
