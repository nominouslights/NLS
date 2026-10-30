using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Inspections.Events;

/// <summary>
/// Raised when the carrier signs the NL-PTI-01 acknowledgement line on a failed inspection
/// (<see cref="VehicleInspection.AcknowledgeAsCarrier"/>).
///
/// It exists for the same reason <see cref="VehicleInspectionDefectsResolvedDomainEvent"/> does:
/// the audit pipeline refuses an eventless aggregate write, so with no domain event there is no
/// <c>event_journal</c> row, the projection worker never sees the save, and
/// <c>rm_vehicle_inspections</c> would keep serving the report as unacknowledged forever.
///
/// Internal to the module — <c>FleetIntegrationEventMapper</c> maps it to null (its default
/// arm): who signed the carrier line does not change Trips' view of the trip.
/// </summary>
public sealed record VehicleInspectionCarrierAcknowledgedDomainEvent(Guid InspectionId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
