using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Application.Trips.Assign;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class AssignTripCommandHandlerTests
{
    private readonly FakeTripRepository _trips = new();
    private readonly FakeDriverLookupRepository _drivers = new();
    private readonly FakeVehicleLookupRepository _vehicles = new();

    private AssignTripCommandHandler Handler => new(_trips, _drivers, _vehicles);

    private static DriverLookup Driver(Guid id, string status = DriverLookup.ActiveStatus) => new()
    {
        DriverId = id,
        TenantId = TestPlanning.TenantId,
        Name = "R. Ballantyne",
        LicenceClass = "Class 4",
        Status = status,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    private static VehicleLookup Vehicle(Guid id, string status = VehicleLookup.ActiveStatus) => new()
    {
        VehicleId = id,
        TenantId = TestPlanning.TenantId,
        UnitNumber = "U-12",
        Status = status,
        RequiredLicenceClass = "Class 4",
        SeatingCapacity = 24,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Missing_trip_is_not_found()
    {
        var result = await Handler.Handle(
            new AssignTripCommand(Guid.NewGuid(), null, null, null), CancellationToken.None);

        Assert.Equal(TripErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Unknown_driver_is_rejected()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);

        var result = await Handler.Handle(
            new AssignTripCommand(trip.Id, Guid.NewGuid(), null, null), CancellationToken.None);

        Assert.Equal(TripErrors.DriverNotFound, result.Error);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Theory]
    [InlineData("Inactive")]
    [InlineData("Deactivated")]
    public async Task Non_active_driver_is_rejected(string status)
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);
        var driverId = Guid.NewGuid();
        _drivers.Drivers.Add(Driver(driverId, status));

        var result = await Handler.Handle(
            new AssignTripCommand(trip.Id, driverId, null, null), CancellationToken.None);

        Assert.Equal(TripErrors.DriverNotActive, result.Error);
        Assert.Null(trip.DriverId);
    }

    [Fact]
    public async Task Unknown_vehicle_is_rejected()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);

        var result = await Handler.Handle(
            new AssignTripCommand(trip.Id, null, Guid.NewGuid(), null), CancellationToken.None);

        Assert.Equal(TripErrors.VehicleNotFound, result.Error);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Theory]
    [InlineData("InMaintenance")]
    [InlineData("OutOfService")]
    [InlineData("Retired")]
    public async Task Non_active_vehicle_is_rejected(string status)
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);
        var vehicleId = Guid.NewGuid();
        _vehicles.Vehicles.Add(Vehicle(vehicleId, status));

        var result = await Handler.Handle(
            new AssignTripCommand(trip.Id, null, vehicleId, null), CancellationToken.None);

        Assert.Equal(TripErrors.VehicleNotActive, result.Error);
        Assert.Null(trip.VehicleId);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Active_vehicle_is_assigned_with_the_lookup_unit_snapshot()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);
        var vehicleId = Guid.NewGuid();
        _vehicles.Vehicles.Add(Vehicle(vehicleId));

        // A stale/free-form unit supplied by the caller is ignored in favour of the snapshot.
        var result = await Handler.Handle(
            new AssignTripCommand(trip.Id, null, vehicleId, "stale-unit"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(vehicleId, trip.VehicleId);
        Assert.Equal("U-12", trip.VehicleUnit);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Active_driver_is_assigned_with_the_lookup_name_snapshot()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);
        var driverId = Guid.NewGuid();
        _drivers.Drivers.Add(Driver(driverId));

        var result = await Handler.Handle(
            new AssignTripCommand(trip.Id, driverId, null, "U-07"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(driverId, trip.DriverId);
        Assert.Equal("R. Ballantyne", trip.DriverName);
        Assert.Equal("U-07", trip.VehicleUnit);
        Assert.Null(trip.VehicleId);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Null_driver_id_unassigns_and_null_vehicle_clears_it()
    {
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var trip = TestPlanning.ScheduleTrip(driverId: driverId, driverName: "R. Ballantyne", vehicleId: vehicleId).Value;
        _trips.Add(trip);

        var result = await Handler.Handle(
            new AssignTripCommand(trip.Id, null, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(trip.DriverId);
        Assert.Null(trip.DriverName);
        Assert.Null(trip.VehicleId);
        Assert.Null(trip.VehicleUnit);
    }
}
