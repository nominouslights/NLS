using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Vehicles.Events;

/// <summary>Raised when an odometer reading crosses the vehicle's end-of-life threshold and it auto-retires.</summary>
public sealed record VehicleReachedEndOfLifeDomainEvent(
    Guid VehicleId,
    int OdometerKm,
    int EndOfLifeKm) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
