using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Documents.Events;

/// <summary>
/// Raised when a compliance document is attached to a vehicle. Every aggregate write must
/// raise an event — an eventless write produces no journal row and the read model silently
/// goes stale. Internal to the module: <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record VehicleDocumentAddedDomainEvent(Guid DocumentId, Guid VehicleId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
