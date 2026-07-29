using NorthernLink.Fleet.Application.Inspections.PropagateOdometer;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Vehicles;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class PropagateInspectionOdometerCommandHandlerTests
{
    private static PropagateInspectionOdometerCommandHandler Handler(
        InMemoryVehicleInspectionRepository inspections,
        InMemoryVehicleRepository vehicles) => new(inspections, vehicles);

    private static PropagateInspectionOdometerCommand Command(VehicleInspection inspection) =>
        new(TestVehicles.TenantId, inspection.Id);

    [Fact]
    public async Task Post_trip_reading_advances_the_vehicle_odometer()
    {
        var vehicles = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.Create(odometerKm: 118_000, endOfLifeKm: 500_000);
        vehicles.Add(vehicle);

        var inspections = new InMemoryVehicleInspectionRepository();
        var inspection = TestInspections.PostTrip(odometerKm: 118_346, vehicleId: vehicle.Id);
        inspections.Add(inspection);

        var result = await Handler(inspections, vehicles).Handle(Command(inspection), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(118_346, vehicle.OdometerKm);
        Assert.Equal(1, vehicles.SaveChangesCallCount);
    }

    [Fact]
    public async Task Non_monotonic_reading_is_ignored_and_the_inspection_still_stands()
    {
        var vehicles = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.Create(odometerKm: 200_000, endOfLifeKm: 500_000);
        vehicles.Add(vehicle);

        var inspections = new InMemoryVehicleInspectionRepository();
        // A pre-trip odometer-in below the current master reading — must not roll the odometer back.
        var inspection = TestInspections.PreTrip(odometerKm: 118_204, vehicleId: vehicle.Id);
        inspections.Add(inspection);

        var result = await Handler(inspections, vehicles).Handle(Command(inspection), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(200_000, vehicle.OdometerKm);
        Assert.Equal(0, vehicles.SaveChangesCallCount);
    }

    [Fact]
    public async Task Crossing_end_of_life_auto_retires_and_issues_a_certificate()
    {
        var vehicles = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.Create(odometerKm: 495_000, acquisitionCostCad: 175_000m, endOfLifeKm: 500_000);
        vehicles.Add(vehicle);

        var inspections = new InMemoryVehicleInspectionRepository();
        var inspection = TestInspections.PostTrip(odometerKm: 501_000, vehicleId: vehicle.Id);
        inspections.Add(inspection);

        var result = await Handler(inspections, vehicles).Handle(Command(inspection), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.Retired, vehicle.Status);
        var certificate = Assert.Single(vehicles.Certificates);
        Assert.Equal(vehicle.Id, certificate.VehicleId);
        Assert.Equal(501_000, certificate.FinalOdometerKm);
    }

    [Fact]
    public async Task Vehicle_is_resolved_by_unit_when_the_inspection_carries_no_vehicle_id()
    {
        var vehicles = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.Create(odometerKm: 118_000, unitNumber: "U-77");
        vehicles.Add(vehicle);

        var inspections = new InMemoryVehicleInspectionRepository();
        var inspection = TestInspections.PostTrip(odometerKm: 118_500, vehicleId: null, unit: "U-77");
        inspections.Add(inspection);

        var result = await Handler(inspections, vehicles).Handle(Command(inspection), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(118_500, vehicle.OdometerKm);
    }

    [Fact]
    public async Task No_matching_vehicle_is_a_graceful_no_op()
    {
        var vehicles = new InMemoryVehicleRepository();

        var inspections = new InMemoryVehicleInspectionRepository();
        var inspection = TestInspections.PostTrip(odometerKm: 118_500, vehicleId: Guid.NewGuid());
        inspections.Add(inspection);

        var result = await Handler(inspections, vehicles).Handle(Command(inspection), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, vehicles.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_inspection_without_an_odometer_reading_is_a_no_op()
    {
        var vehicles = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.Create(odometerKm: 118_000);
        vehicles.Add(vehicle);

        var inspections = new InMemoryVehicleInspectionRepository();
        var inspection = TestInspections.PostTrip(odometerKm: null, vehicleId: vehicle.Id);
        inspections.Add(inspection);

        var result = await Handler(inspections, vehicles).Handle(Command(inspection), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(118_000, vehicle.OdometerKm);
        Assert.Equal(0, vehicles.SaveChangesCallCount);
    }
}
