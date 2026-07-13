using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Vehicles.Events;

/// <summary>
/// Raised on every status transition. The future publish hook for a
/// <c>fleet.vehicle-status-changed</c> integration event once Trips consumes it.
/// </summary>
public sealed record VehicleStatusChangedDomainEvent(
    Guid VehicleId,
    VehicleStatus PreviousStatus,
    VehicleStatus NewStatus,
    string? Reason) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
