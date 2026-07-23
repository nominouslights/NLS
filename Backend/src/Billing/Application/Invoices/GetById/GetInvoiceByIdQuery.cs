using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.GetById;

/// <summary>Full invoice detail, lines included.</summary>
public sealed record GetInvoiceByIdQuery(Guid TenantId, Guid InvoiceId) : IQuery<InvoiceResponse>;
