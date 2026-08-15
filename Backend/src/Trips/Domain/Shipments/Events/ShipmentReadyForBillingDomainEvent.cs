using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Shipments.Events;

/// <summary>
/// Raised when a shipment is handed over carrying a client and a charge — the publish hook for
/// the <c>trips.shipment-ready-for-billing</c> integration event Billing consumes to record a
/// billable shipment.
/// <para>
/// <b>The client on that shipment is the shipment's own, and is routinely not the client of the
/// trip that carried it</b> — a run for one client can be full of another's freight. That is the
/// entire reason cargo bills on its own invoice rather than as lines on the trip's, and it is
/// the invariant every consumer of this event depends on.
/// </para>
/// <para>
/// Also re-raised by <c>SetBilling</c> when a delivered-but-clientless shipment is later
/// attributed to a client, or when a charge is corrected before an invoice claims it. Billing's
/// handler refreshes an uninvoiced replica row and leaves a claimed one alone, so the repeat is
/// safe.
/// </para>
/// </summary>
public sealed record ShipmentReadyForBillingDomainEvent(Guid ShipmentId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
