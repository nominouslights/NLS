using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.MarkEntered;

/// <summary>Records that a draft worksheet has been keyed into QuickBooks Online — no QBO API calls anywhere.</summary>
public sealed record MarkInvoiceEnteredCommand(
    Guid TenantId,
    Guid InvoiceId,
    string QboInvoiceNumber,
    DateOnly EnteredDate) : ICommand;
