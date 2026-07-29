using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Fleet;

/// <summary>
/// Published whenever a Fleet vehicle inspection is hard-deleted — routing key
/// <c>fleet.vehicle-inspection-removed</c>. The counterpart to
/// <see cref="VehicleInspectionRecordedIntegrationEvent"/>: Trips consumes a <c>PostTrip</c>
/// removal carrying a trip number to re-gate that trip's completion (clearing the
/// post-trip-inspection flag so <c>Trip.Complete()</c> refuses again). Pre-trip removals and
/// removals with no trip context are ignored by Trips. <see cref="InspectionType"/> travels as
/// a string ("PreTrip"/"PostTrip") — integration events never reference Fleet's internal enums.
/// <see cref="TenantId"/> is part of the payload because handlers run outside any HTTP request,
/// and the inspection row is already gone by the time the handler runs, so everything the
/// consumer needs is carried here. Delivery is at-least-once; the flag clear is idempotent.
/// </summary>
public sealed record VehicleInspectionRemovedIntegrationEvent(
    Guid InspectionId,
    Guid TenantId,
    string? TripNumber,
    string InspectionType) : IntegrationEvent;
