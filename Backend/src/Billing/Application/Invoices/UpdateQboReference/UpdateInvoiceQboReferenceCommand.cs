using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.UpdateQboReference;

/// <summary>Corrects the recorded QuickBooks Online reference on an already-entered worksheet — no QBO API calls anywhere.</summary>
public sealed record UpdateInvoiceQboReferenceCommand(
    Guid TenantId,
    Guid InvoiceId,
    string QboInvoiceNumber,
    DateOnly EnteredDate) : ICommand;
