using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Inspections.Events;

/// <summary>
/// Raised when one defect on this inspection is attached to, or released from, an active work
/// order (<see cref="VehicleInspection.AssignDefectToWorkOrder"/>,
/// <see cref="VehicleInspection.ReleaseDefectFromWorkOrder"/>, or the release half of
/// <see cref="VehicleInspection.ResolveDefectUnderWorkOrder"/> on an already-resolved defect).
/// <paramref name="WorkOrderId"/> is the defect's new active work order — null on a release.
///
/// Exists because the audit pipeline rejects an eventless aggregate write: without it the
/// jsonb change would never reach <c>event_journal</c> and <c>rm_vehicle_inspections</c> would
/// keep serving the old link. Internal to the module — <c>FleetIntegrationEventMapper</c> maps it
/// to null (its default arm); nothing outside Fleet consumes work orders.
/// </summary>
public sealed record VehicleInspectionDefectWorkOrderChangedDomainEvent(
    Guid InspectionId,
    Guid TenantId,
    string Item,
    Guid? WorkOrderId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
