using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Fleet;

/// <summary>
/// Published whenever a Fleet vehicle inspection is recorded — routing key
/// <c>fleet.vehicle-inspection-recorded</c>. One event covers both pre- and post-trip
/// inspections; consumers filter on <see cref="InspectionType"/> ("PreTrip"/"PostTrip").
/// Trips consumes it to satisfy its "a trip cannot Complete without a logged post-trip
/// inspection" rule without a library reference: a matching <see cref="TripNumber"/> flips
/// the trip's post-trip-inspection flag. Delivery is at-least-once, so handlers key off the
/// idempotent flip. <see cref="TripNumber"/>/<see cref="VehicleId"/> are nullable because an
/// inspection can be a standalone vehicle entry with no trip or asset link. Enum-typed fields
/// travel as strings: integration events are the module's public contract and never reference
/// Fleet's internal enums. <see cref="TenantId"/> is part of the payload because handlers run
/// outside any HTTP request. Mirrors <c>Fleet/VehicleChangedIntegrationEvent</c>.
/// </summary>
public sealed record VehicleInspectionRecordedIntegrationEvent(
    Guid InspectionId,
    Guid TenantId,
    string? TripNumber,
    string InspectionType,
    Guid? VehicleId,
    string Source) : IntegrationEvent;
