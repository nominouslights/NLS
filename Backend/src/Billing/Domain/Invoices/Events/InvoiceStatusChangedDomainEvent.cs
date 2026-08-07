using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.Invoices.Events;

/// <summary>
/// Raised on the lifecycle transitions with no more specific event of their own — today just
/// Draft→Void. (EnteredInQbo→Draft used to travel here too, before entry into QuickBooks became
/// one-way; a write-off has its own event because it carries an amount and a reason.)
/// </summary>
public sealed record InvoiceStatusChangedDomainEvent(
    Guid InvoiceId,
    InvoiceStatus PreviousStatus,
    InvoiceStatus NewStatus) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
