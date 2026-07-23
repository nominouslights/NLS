using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Vehicles.Events;

/// <summary>
/// Raised when a vehicle's registration details are edited. Internal to the module:
/// <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record VehicleDetailsUpdatedDomainEvent(Guid VehicleId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
