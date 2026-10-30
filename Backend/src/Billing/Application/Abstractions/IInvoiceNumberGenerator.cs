namespace NorthernLink.Billing.Application.Abstractions;

/// <summary>
/// Per-tenant invoice numbering. Reserves a whole batch in one call because one generation
/// request now produces one worksheet per purchase order: numbering derives from the count of
/// persisted invoices, so calling a single-number method N times before SaveChanges would hand
/// back the same number N times.
/// </summary>
public interface IInvoiceNumberGenerator
{
    /// <summary>
    /// The next <paramref name="count"/> invoice numbers, in order, all unique within the
    /// batch and continuing the tenant's sequence.
    /// </summary>
    Task<IReadOnlyList<string>> NextInvoiceNumbersAsync(
        Guid tenantId, int count, CancellationToken cancellationToken = default);
}
