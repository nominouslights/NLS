using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.MarkPaid;

/// <summary>Marks a sent invoice as paid.</summary>
public sealed record MarkInvoicePaidCommand(Guid TenantId, Guid InvoiceId) : ICommand;
