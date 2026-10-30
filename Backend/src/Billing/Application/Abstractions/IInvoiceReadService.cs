using NorthernLink.Billing.Application.Invoices;
using NorthernLink.Billing.Domain.Invoices;

namespace NorthernLink.Billing.Application.Abstractions;

/// <summary>Read side — the invoice list from <c>rm_invoices</c> (detail reads the aggregate
/// for read-your-writes line editing; see GetInvoiceByIdQueryHandler).</summary>
public interface IInvoiceReadService
{
    Task<IReadOnlyList<InvoiceSummaryResponse>> GetInvoicesAsync(
        InvoiceStatus? status,
        Guid? clientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// What has already been invoiced against each of a client's PO numbers — the sum of every
    /// non-Void worksheet total, keyed case-insensitively by PO number. Feeds the PO-value
    /// warning during draft generation and nothing else; a void worksheet never drew the PO
    /// down, so it is excluded, while a draft does count (it is money about to be claimed).
    /// Read from <c>rm_invoices</c>, which is projected asynchronously, so a worksheet created
    /// seconds ago may not be included yet — acceptable for a warning, never for a refusal.
    /// </summary>
    Task<IReadOnlyDictionary<string, decimal>> GetInvoicedTotalsByPoNumberAsync(
        Guid clientId,
        CancellationToken cancellationToken = default);
}
