using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.ClearPayment;

/// <summary>Clears a payment confirmation recorded in error, returning the worksheet to entered-in-QBO.</summary>
public sealed record ClearInvoicePaymentCommand(Guid TenantId, Guid InvoiceId) : ICommand;
