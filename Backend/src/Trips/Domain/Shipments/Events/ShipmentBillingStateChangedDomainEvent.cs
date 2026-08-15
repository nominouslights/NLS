using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Shipments.Events;

/// <summary>
/// A billing-driven lifecycle move — Invoiced, Settled, or WrittenOff — applied from Billing's
/// <c>billing.invoice-billing-state-changed</c> event.
/// <para>
/// Internal only, and it must stay that way: these facts originated in Billing, so republishing
/// them would be a loop. It exists so the journal records <em>why</em> a shipment's status moved
/// without a dispatcher touching it.
/// </para>
/// </summary>
public sealed record ShipmentBillingStateChangedDomainEvent(
    Guid ShipmentId,
    ShipmentStatus Status) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
