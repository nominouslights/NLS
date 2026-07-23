using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.Send;

/// <summary>Marks a draft invoice as sent — the moment net terms start counting.</summary>
public sealed record SendInvoiceCommand(Guid TenantId, Guid InvoiceId) : ICommand;
