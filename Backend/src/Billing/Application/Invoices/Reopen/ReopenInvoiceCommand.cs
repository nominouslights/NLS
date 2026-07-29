using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.Reopen;

/// <summary>Reopens an entered worksheet back to Draft for further editing; clears the recorded QBO reference.</summary>
public sealed record ReopenInvoiceCommand(Guid TenantId, Guid InvoiceId) : ICommand;
