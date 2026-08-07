using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when a dispatcher closes out a ReadyForBilling trip that will never be invoiced —
/// distinct from <see cref="TripWrittenOffDomainEvent"/> (Billing writing off an invoice) even
/// though both land in WrittenOff, because this one is the only write-off that originates in
/// Trips and therefore the only one Billing needs to be told about: it publishes
/// <c>trips.trip-closed-without-billing</c> so Billing can drop the trip from the billable pool.
/// Mapping the invoice-driven event instead would just echo Billing's own fact back at it.
/// </summary>
public sealed record TripClosedWithoutBillingDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
