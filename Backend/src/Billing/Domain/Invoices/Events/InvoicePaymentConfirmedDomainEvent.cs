using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.Invoices.Events;

/// <summary>
/// Raised when payment against an entered worksheet is confirmed by hand, and again when that
/// confirmation is cleared (<see cref="ConfirmedDate"/> null) because it was recorded in error.
/// Both directions carry the same event so the read model, the audit trail, and the Trips
/// billing replica all move off one signal.
/// </summary>
public sealed record InvoicePaymentConfirmedDomainEvent(
    Guid InvoiceId,
    DateOnly? ConfirmedDate) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
