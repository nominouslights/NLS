using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.Invoices.SetQboStatus;

/// <summary>Records manual QuickBooks Online reconciliation state — no QBO API calls anywhere.</summary>
public sealed record SetInvoiceQboStatusCommand(
    Guid TenantId,
    Guid InvoiceId,
    string? QboInvoiceId,
    QboSyncStatus SyncStatus) : ICommand;
