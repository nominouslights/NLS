using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Shipments.Events;

/// <summary>Raised when the freight is loaded onto one leg's trip.</summary>
public sealed record ShipmentLegPickedUpDomainEvent(Guid ShipmentId, int Sequence) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
