using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Trips.Events;

/// <summary>
/// Raised when a trip reaches Completed — which now means the money arrived: payment confirmed
/// against its QuickBooks invoice, or a clientless run (community, walk-up charter) finishing its
/// run with the fare already collected.
/// <para>
/// No longer Billing's feed. That role moved to <see cref="TripReadyForBillingDomainEvent"/>,
/// which fires when the run ends — by the time this one fires there is nothing left to invoice.
/// This event is internal to Trips and publishes nothing.
/// </para>
/// </summary>
public sealed record TripCompletedDomainEvent(Guid TripId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
