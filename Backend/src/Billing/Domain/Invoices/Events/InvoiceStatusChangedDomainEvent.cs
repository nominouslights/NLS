using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.Invoices.Events;

/// <summary>Raised on every lifecycle transition (Draft→Sent, Sent→Paid, Draft→Void).</summary>
public sealed record InvoiceStatusChangedDomainEvent(
    Guid InvoiceId,
    InvoiceStatus PreviousStatus,
    InvoiceStatus NewStatus) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
