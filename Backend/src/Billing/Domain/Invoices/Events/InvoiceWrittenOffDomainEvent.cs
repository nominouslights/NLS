using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.Invoices.Events;

/// <summary>
/// Raised when an entered worksheet is written off — the client will not pay and the balance is
/// zeroed. The publish hook for a <c>billing.invoice-billing-state-changed</c> event carrying
/// <c>WrittenOff</c>, which moves every trip on the invoice to its own terminal WrittenOff.
/// <para>
/// Unlike a void, this does <em>not</em> release the invoice's claimed trips: the work happened
/// and was billed once, so letting those trips back into the billable pool would invite a second
/// invoice for the same runs.
/// </para>
/// </summary>
public sealed record InvoiceWrittenOffDomainEvent(
    Guid InvoiceId,
    decimal AmountCad,
    DateOnly WrittenOffDate,
    string Reason) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
