using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Shipments.Events;

/// <summary>
/// Raised when a shipment is handed over and <em>nothing will be invoiced for it</em> — it has
/// no client (a counter sale, settled at the desk) or no charge (a goodwill carry). The exact
/// counterpart of a clientless trip finishing straight into Completed.
/// <para>
/// Internal only. A shipment that later acquires a client through <c>SetBilling</c> raises
/// <see cref="ShipmentReadyForBillingDomainEvent"/> at that point instead — which is the path
/// every backfilled legacy cargo row has to travel, since the old jsonb never recorded a client.
/// </para>
/// </summary>
public sealed record ShipmentDeliveredDomainEvent(Guid ShipmentId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
