using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Shipments.Events;

/// <summary>
/// Raised when the freight comes off one leg's trip — at a hub with another leg still to run,
/// or at its final destination. Distinct from <see cref="ShipmentDeliveredDomainEvent"/>: a
/// driver unloading at a transfer point and a consignee signing for goods are not the same act,
/// and only the second one earns the charge.
/// </summary>
public sealed record ShipmentLegDroppedDomainEvent(Guid ShipmentId, int Sequence) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
