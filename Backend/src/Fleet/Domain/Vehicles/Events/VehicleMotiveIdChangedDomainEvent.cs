using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Vehicles.Events;

/// <summary>
/// Raised when a vehicle's Motive id is set, changed or cleared — i.e. when the bus becomes, or
/// stops being, a regulated vehicle whose hours Motive records. Its own event rather than a
/// <see cref="VehicleDetailsUpdatedDomainEvent"/> so the journal shows the regulated flag
/// changing distinctly from a roster edit. Internal to the module for now:
/// <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record VehicleMotiveIdChangedDomainEvent(
    Guid VehicleId,
    Guid TenantId,
    string? PreviousMotiveVehicleId,
    string? MotiveVehicleId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
