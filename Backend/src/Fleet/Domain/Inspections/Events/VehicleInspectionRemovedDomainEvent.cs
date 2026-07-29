using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Inspections.Events;

/// <summary>
/// Raised immediately before an inspection is hard-deleted, so the removal reaches consumers:
/// <c>FleetIntegrationEventMapper</c> maps it to the public
/// <c>fleet.vehicle-inspection-removed</c> integration event, which lets Trips re-gate
/// completion when a post-trip inspection is deleted. The synthetic <c>aggregate-deleted</c>
/// journal row (written by the audit pipeline for every hard delete) already drives the
/// read-model deletion; this explicit event exists purely to carry the removal across the
/// module boundary, since the mapper only ever sees real domain events. Carries the tenant and
/// (via the aggregate) the trip number/type the mapper needs, because the row is gone by the
/// time any downstream reaction runs.
/// </summary>
public sealed record VehicleInspectionRemovedDomainEvent(Guid InspectionId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
