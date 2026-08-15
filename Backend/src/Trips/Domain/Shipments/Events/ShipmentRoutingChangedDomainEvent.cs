using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Shipments.Events;

/// <summary>
/// Raised whenever the set of trips a shipment rides changes — a leg added, or a planned leg
/// removed (including the removal a cancelled trip triggers). The projection rebuilds the
/// shipment's leg rows off this, which is what keeps a trip manifest's cargo section honest.
/// </summary>
public sealed record ShipmentRoutingChangedDomainEvent(Guid ShipmentId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
