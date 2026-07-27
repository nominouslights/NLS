using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Shared.Kernel;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Inspections.Events;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Domain.Vehicles.Events;

namespace NorthernLink.Fleet.Application.Vehicles;

/// <summary>
/// Fleet's explicit domain-event → integration-event translation. Register, update, and
/// status change all map to the single upsert-shaped <c>fleet.vehicle-changed</c> event —
/// consumers (Trips' <c>vehicle_lookup</c>) maintain a replica keyed on VehicleId, so one
/// event carrying current state covers all three and new/renamed vehicles reach Trips.
/// A recorded inspection maps to <c>fleet.vehicle-inspection-recorded</c> (both pre- and
/// post-trip; Trips filters), which lets Trips enforce its post-trip-inspection gate on
/// completion without a library reference. Odometer/end-of-life/disposal detail stays
/// internal (null). Extending Fleet's public surface means adding a case here plus an event
/// record in NorthernLink.Shared/IntegrationEvents/Fleet/ — never auto-publishing.
/// </summary>
public sealed class FleetIntegrationEventMapper : IIntegrationEventMapper
{
    // Fleet models no per-vehicle operator/source (unlike Driver.Source); the internal
    // operator is the platform itself. Carried for contract parity — Trips' vehicle_lookup
    // does not consume it.
    private const string InternalSource = "Northern Link";

    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate) =>
        domainEvent switch
        {
            VehicleRegisteredDomainEvent or VehicleDetailsUpdatedDomainEvent or VehicleStatusChangedDomainEvent =>
                ToVehicleChanged((Vehicle)aggregate),
            VehicleInspectionCreatedDomainEvent =>
                ToInspectionRecorded((VehicleInspection)aggregate),
            _ => null,
        };

    private static VehicleChangedIntegrationEvent ToVehicleChanged(Vehicle vehicle) => new(
        vehicle.Id,
        vehicle.TenantId,
        vehicle.UnitNumber,
        vehicle.Status.ToString(),
        vehicle.RequiredLicenceClass,
        vehicle.SeatingCapacity,
        InternalSource);

    private static VehicleInspectionRecordedIntegrationEvent ToInspectionRecorded(VehicleInspection inspection) => new(
        inspection.Id,
        inspection.TenantId,
        inspection.TripNumber,
        inspection.Type.ToString(),
        inspection.VehicleId,
        inspection.Source.ToString());
}
