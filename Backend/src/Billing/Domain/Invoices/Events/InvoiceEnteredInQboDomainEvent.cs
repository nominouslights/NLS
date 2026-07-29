using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.Invoices.Events;

/// <summary>Raised when a worksheet is entered in QBO, or its recorded QBO reference is corrected.</summary>
public sealed record InvoiceEnteredInQboDomainEvent(
    Guid InvoiceId,
    string QboInvoiceId,
    DateOnly EnteredDate) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
