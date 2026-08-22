using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Shipments.Events;

/// <summary>
/// The undo of the billable feed: the dispatcher has decided this shipment will never be
/// invoiced, so Billing must drop it from its uninvoiced pool. Published as
/// <c>trips.shipment-closed-without-billing</c>.
/// <para>
/// Only the dispatcher's close-without-billing raises this. A write-off driven by an invoice
/// stays internal — that fact came from Billing to begin with, and echoing it back would loop.
/// </para>
/// </summary>
public sealed record ShipmentClosedWithoutBillingDomainEvent(Guid ShipmentId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
