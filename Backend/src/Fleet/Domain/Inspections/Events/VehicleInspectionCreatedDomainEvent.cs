using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Inspections.Events;

/// <summary>
/// Raised when an inspection record is materialized — from a completed trip manifest or a
/// dispatcher paper-backup entry. Every aggregate write must raise an event — an eventless
/// write produces no journal row and the read model silently goes stale. Drives two reactions
/// off this one event: the intra-Fleet odometer projection (same module) and, via
/// <c>FleetIntegrationEventMapper</c>, the public <c>fleet.vehicle-inspection-recorded</c>
/// integration event (Trips' post-trip-inspection completion gate).
/// </summary>
public sealed record VehicleInspectionCreatedDomainEvent(Guid InspectionId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
