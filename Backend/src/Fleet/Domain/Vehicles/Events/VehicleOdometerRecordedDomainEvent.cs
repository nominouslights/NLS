using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Vehicles.Events;

/// <summary>
/// Raised on every odometer reading (the end-of-life crossing additionally raises
/// <see cref="VehicleReachedEndOfLifeDomainEvent"/>). Internal to the module:
/// <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record VehicleOdometerRecordedDomainEvent(Guid VehicleId, int OdometerKm) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
