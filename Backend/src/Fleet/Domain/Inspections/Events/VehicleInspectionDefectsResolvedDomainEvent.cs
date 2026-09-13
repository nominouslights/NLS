using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Inspections.Events;

/// <summary>
/// Raised when one or more of this inspection's defects are stamped resolved — either by a
/// dispatcher clearing a single item (<see cref="VehicleInspection.ResolveDefect"/>) or by the
/// work order generated from this inspection being completed
/// (<see cref="VehicleInspection.ResolveDefectsForWorkOrder"/>).
///
/// It exists because the audit pipeline refuses an eventless aggregate write: with no domain
/// event there is no <c>event_journal</c> row, the projection worker never sees the save, and
/// <c>rm_vehicle_inspections</c> would keep serving the defect as open forever. The plan for
/// this feature did not call for an event; the pipeline's guard does.
///
/// Internal to the module — <c>FleetIntegrationEventMapper</c> maps it to null (its default
/// arm): resolving a defect does not change Trips' view of the trip, and the read-model
/// re-projection is keyed on the aggregate, not the event type.
/// </summary>
public sealed record VehicleInspectionDefectsResolvedDomainEvent(Guid InspectionId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
