using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.GetInvoices;

/// <summary>Invoice list (Billing screen), optionally filtered by status and/or client.</summary>
public sealed record GetInvoicesQuery(
    Guid TenantId,
    InvoiceStatus? Status,
    Guid? ClientId) : IQuery<IReadOnlyList<InvoiceSummaryResponse>>;
