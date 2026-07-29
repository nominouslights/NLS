using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Inspections.Events;

/// <summary>
/// Raised when an existing inspection's contents are corrected (an amend) — the odometer,
/// driver, checklist, defects, or either section. Identity (<c>Type</c>, <c>TripNumber</c>)
/// never changes, so this is not a re-recording: Trips' completion gate already flipped on the
/// original <c>VehicleInspectionCreatedDomainEvent</c> and stays flipped. Drives two reactions
/// like the create: the intra-Fleet odometer projection (a corrected odometer still flows to the
/// vehicle) and the read-model re-projection. <c>FleetIntegrationEventMapper</c> maps it to null
/// — an amend does not change Trips' view of the trip.
/// </summary>
public sealed record VehicleInspectionAmendedDomainEvent(Guid InspectionId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
