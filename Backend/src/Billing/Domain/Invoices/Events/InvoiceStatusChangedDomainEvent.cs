using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.Invoices.Events;

/// <summary>Raised on lifecycle transitions carried by this event: Draft→Void and EnteredInQbo→Draft (Reopen).</summary>
public sealed record InvoiceStatusChangedDomainEvent(
    Guid InvoiceId,
    InvoiceStatus PreviousStatus,
    InvoiceStatus NewStatus) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
