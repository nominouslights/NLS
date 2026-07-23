using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.Void;

/// <summary>Voids a draft invoice and releases every billable trip it had claimed.</summary>
public sealed record VoidInvoiceCommand(Guid TenantId, Guid InvoiceId) : ICommand;
