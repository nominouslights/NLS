using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Vehicles.Events;

/// <summary>Raised when a vehicle is first registered in the fleet.</summary>
public sealed record VehicleRegisteredDomainEvent(Guid VehicleId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
