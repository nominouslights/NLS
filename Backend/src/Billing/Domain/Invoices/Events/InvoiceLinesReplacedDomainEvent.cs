using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.Invoices.Events;

/// <summary>Raised on every draft line edit, so the read model and audit trail track totals.</summary>
public sealed record InvoiceLinesReplacedDomainEvent(
    Guid InvoiceId,
    int LineCount,
    decimal SubtotalCad,
    decimal TotalCad) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
