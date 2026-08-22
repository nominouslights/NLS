using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Shipments.Events;

/// <summary>
/// Catch-all lifecycle move that has no more specific event of its own — cancellation, and the
/// return to Registered when a cancelled trip releases its planned legs. Internal only.
/// </summary>
public sealed record ShipmentStatusChangedDomainEvent(Guid ShipmentId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
