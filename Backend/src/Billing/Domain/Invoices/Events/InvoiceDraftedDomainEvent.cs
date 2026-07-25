using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.Invoices.Events;

/// <summary>Raised when a draft invoice is created (generated or hand-started).</summary>
public sealed record InvoiceDraftedDomainEvent(
    Guid InvoiceId,
    Guid TenantId,
    string InvoiceNumber,
    Guid ClientId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
