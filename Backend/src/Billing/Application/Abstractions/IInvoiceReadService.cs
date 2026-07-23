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
}
