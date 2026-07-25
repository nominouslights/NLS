using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Inspections.Events;

/// <summary>
/// Raised when an inspection record is materialized — from a completed trip manifest or a
/// dispatcher paper-backup entry. Every aggregate write must raise an event — an eventless
/// write produces no journal row and the read model silently goes stale. Internal to the
/// module: <c>FleetIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record VehicleInspectionCreatedDomainEvent(Guid InspectionId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
