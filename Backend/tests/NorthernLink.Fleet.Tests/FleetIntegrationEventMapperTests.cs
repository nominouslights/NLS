using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Inspections.Events;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Domain.Vehicles.Events;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class FleetIntegrationEventMapperTests
{
    private readonly FleetIntegrationEventMapper _mapper = new();

    [Fact]
    public void Registration_maps_to_the_vehicle_changed_event_with_current_state()
    {
        var vehicle = TestVehicles.Create();
        var domainEvent = new VehicleRegisteredDomainEvent(vehicle.Id, vehicle.TenantId);

        var result = _mapper.Map(domainEvent, vehicle);

        var integrationEvent = Assert.IsType<VehicleChangedIntegrationEvent>(result);
        Assert.Equal(vehicle.Id, integrationEvent.VehicleId);
        Assert.Equal(vehicle.TenantId, integrationEvent.TenantId);
        Assert.Equal("U-99", integrationEvent.UnitNumber);
        Assert.Equal("Active", integrationEvent.Status);
        Assert.Equal("Class 4", integrationEvent.RequiredLicenceClass);
        Assert.Equal(24, integrationEvent.SeatingCapacity);
        Assert.Equal("Northern Link", integrationEvent.Source);
    }

    [Fact]
    public void Update_maps_to_the_vehicle_changed_event_with_the_new_details()
    {
        var vehicle = TestVehicles.Create();
        Assert.True(vehicle.UpdateDetails(
            "U-77",
            Vin.Create(TestVehicles.ValidVin).Value,
            "International",
            "3000",
            2019,
            seatingCapacity: 30,
            "MB · GHT 999",
            "Class 2",
            acquisitionCostCad: 100_000m,
            endOfLifeKm: 500_000).IsSuccess);
        var domainEvent = new VehicleDetailsUpdatedDomainEvent(vehicle.Id, vehicle.TenantId);

        var integrationEvent = Assert.IsType<VehicleChangedIntegrationEvent>(_mapper.Map(domainEvent, vehicle));
        Assert.Equal("U-77", integrationEvent.UnitNumber);
        Assert.Equal("Class 2", integrationEvent.RequiredLicenceClass);
        Assert.Equal(30, integrationEvent.SeatingCapacity);
    }

    [Fact]
    public void Status_change_maps_to_the_vehicle_changed_event_with_the_new_status()
    {
        var vehicle = TestVehicles.Create();
        Assert.True(vehicle.ChangeStatus(VehicleStatus.InMaintenance, "Brake inspection").IsSuccess);
        var domainEvent = new VehicleStatusChangedDomainEvent(
            vehicle.Id, VehicleStatus.Active, VehicleStatus.InMaintenance, "Brake inspection");

        var integrationEvent = Assert.IsType<VehicleChangedIntegrationEvent>(_mapper.Map(domainEvent, vehicle));
        Assert.Equal(vehicle.Id, integrationEvent.VehicleId);
        Assert.Equal("InMaintenance", integrationEvent.Status);
    }

    [Fact]
    public void Other_domain_events_stay_internal()
    {
        var vehicle = TestVehicles.Create();

        Assert.Null(_mapper.Map(new VehicleOdometerRecordedDomainEvent(vehicle.Id, OdometerKm: 120_000), vehicle));
        Assert.Null(_mapper.Map(
            new VehicleReachedEndOfLifeDomainEvent(vehicle.Id, OdometerKm: 500_000, EndOfLifeKm: 500_000), vehicle));
    }

    [Fact]
    public void Post_trip_inspection_maps_to_the_inspection_recorded_event()
    {
        var vehicleId = Guid.NewGuid();
        var inspection = TestInspections.PostTrip(vehicleId: vehicleId);
        var domainEvent = new VehicleInspectionCreatedDomainEvent(inspection.Id, inspection.TenantId);

        var integrationEvent = Assert.IsType<VehicleInspectionRecordedIntegrationEvent>(_mapper.Map(domainEvent, inspection));

        Assert.Equal(inspection.Id, integrationEvent.InspectionId);
        Assert.Equal(inspection.TenantId, integrationEvent.TenantId);
        Assert.Equal("TR-4818", integrationEvent.TripNumber);
        Assert.Equal("PostTrip", integrationEvent.InspectionType);
        Assert.Equal(vehicleId, integrationEvent.VehicleId);
        Assert.Equal("Dispatcher", integrationEvent.Source);
    }

    [Fact]
    public void Pre_trip_inspection_also_publishes_the_inspection_recorded_event()
    {
        // Both halves publish; Trips filters on InspectionType.
        var inspection = TestInspections.PreTrip();
        var domainEvent = new VehicleInspectionCreatedDomainEvent(inspection.Id, inspection.TenantId);

        var integrationEvent = Assert.IsType<VehicleInspectionRecordedIntegrationEvent>(_mapper.Map(domainEvent, inspection));

        Assert.Equal("PreTrip", integrationEvent.InspectionType);
    }
}
