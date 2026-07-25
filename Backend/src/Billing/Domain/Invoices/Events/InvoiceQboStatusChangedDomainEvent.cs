using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.Invoices.Events;

/// <summary>Raised when the manual QBO reconciliation flag or reference changes.</summary>
public sealed record InvoiceQboStatusChangedDomainEvent(
    Guid InvoiceId,
    string? QboInvoiceId,
    QboSyncStatus SyncStatus) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
