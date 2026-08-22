using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Shipments.Events;

/// <summary>
/// Raised when cargo is first recorded — which routinely happens before any trip exists to
/// carry it. Internal only: nothing outside Trips cares that a parcel was booked in, and the
/// billing feed hangs off delivery, not registration.
/// </summary>
public sealed record ShipmentRegisteredDomainEvent(Guid ShipmentId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
