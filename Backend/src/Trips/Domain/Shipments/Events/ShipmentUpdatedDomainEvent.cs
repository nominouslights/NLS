using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Shipments.Events;

/// <summary>
/// Raised on every edit to a shipment's descriptive detail, stamped with the editing
/// <see cref="Source"/> and <see cref="EnteredBy"/>. That event stream is the audit log — the
/// same journalled-edit convention <c>TripManifestUpdatedDomainEvent</c> established.
/// </summary>
public sealed record ShipmentUpdatedDomainEvent(
    Guid ShipmentId,
    ShipmentSource Source,
    string? EnteredBy) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
