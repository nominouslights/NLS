using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Application.Trips.Create;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class CreateTripCommandHandlerTests
{
    private readonly FakeTripRepository _trips = new();
    private readonly FakeRouteRepository _routes = new();
    private readonly FakeDriverLookupRepository _drivers = new();
    private readonly FakeVehicleLookupRepository _vehicles = new();
    private readonly FakeTripNumberGenerator _numbers = new();

    private CreateTripCommandHandler Handler => new(_trips, _routes, _drivers, _vehicles, _numbers);

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

    /// <summary>Registers an Active driver + vehicle and returns their ids.</summary>
    private (Guid DriverId, Guid VehicleId) AddActiveAssignment()
    {
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        _drivers.Drivers.Add(Driver(driverId));
        _vehicles.Vehicles.Add(Vehicle(vehicleId));
        return (driverId, vehicleId);
    }

    private static CreateTripCommand Command(Guid? driverId = null, Guid? vehicleId = null) => new(
        TestPlanning.TenantId,
        ServiceDate: new DateOnly(2026, 7, 21),
        WindowStart: new TimeOnly(6, 30),
        WindowEnd: new TimeOnly(8, 15),
        TripServiceType.ContractCrew,
        RouteId: null,
        RouteName: "Thompson ↔ Lynn Lake",
        Origin: "Thompson",
        Destination: "Lynn Lake",
        Stops: TestPlanning.Stops(),
        DistanceKm: 320,
        Direction: null,
        IsEmptyLeg: false,
        ClientId: null,
        ClientName: "Alamos Gold",
        PoNumber: "PO-2026-118",
        DriverId: driverId,
        VehicleId: vehicleId,
        SeatsMinimum: null);

    [Fact]
    public async Task A_trip_without_a_driver_is_rejected()
    {
        var vehicleId = Guid.NewGuid();
        _vehicles.Vehicles.Add(Vehicle(vehicleId));

        var result = await Handler.Handle(Command(vehicleId: vehicleId), CancellationToken.None);

        Assert.Equal(TripErrors.DriverRequired, result.Error);
        Assert.Empty(_trips.Trips);
    }

    [Fact]
    public async Task A_trip_without_a_vehicle_is_rejected()
    {
        var driverId = Guid.NewGuid();
        _drivers.Drivers.Add(Driver(driverId));

        var result = await Handler.Handle(Command(driverId: driverId), CancellationToken.None);

        Assert.Equal(TripErrors.VehicleRequired, result.Error);
        Assert.Empty(_trips.Trips);
    }

    [Fact]
    public async Task Unknown_driver_is_rejected()
    {
        var result = await Handler.Handle(
            Command(driverId: Guid.NewGuid(), vehicleId: Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(TripErrors.DriverNotFound, result.Error);
        Assert.Empty(_trips.Trips);
    }

    [Theory]
    [InlineData("Inactive")]
    [InlineData("Deactivated")]
    public async Task Non_active_driver_is_rejected(string status)
    {
        var driverId = Guid.NewGuid();
        _drivers.Drivers.Add(Driver(driverId, status));

        var result = await Handler.Handle(
            Command(driverId: driverId, vehicleId: Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(TripErrors.DriverNotActive, result.Error);
        Assert.Empty(_trips.Trips);
    }

    [Fact]
    public async Task Unknown_vehicle_is_rejected()
    {
        var driverId = Guid.NewGuid();
        _drivers.Drivers.Add(Driver(driverId));

        var result = await Handler.Handle(
            Command(driverId: driverId, vehicleId: Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(TripErrors.VehicleNotFound, result.Error);
        Assert.Empty(_trips.Trips);
    }

    [Theory]
    [InlineData("InMaintenance")]
    [InlineData("OutOfService")]
    [InlineData("Retired")]
    public async Task Non_active_vehicle_is_rejected(string status)
    {
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        _drivers.Drivers.Add(Driver(driverId));
        _vehicles.Vehicles.Add(Vehicle(vehicleId, status));

        var result = await Handler.Handle(
            Command(driverId: driverId, vehicleId: vehicleId), CancellationToken.None);

        Assert.Equal(TripErrors.VehicleNotActive, result.Error);
        Assert.Empty(_trips.Trips);
    }

    [Fact]
    public async Task Driver_and_vehicle_are_snapshotted_onto_the_trip_from_the_lookups()
    {
        var (driverId, vehicleId) = AddActiveAssignment();

        var result = await Handler.Handle(
            Command(driverId: driverId, vehicleId: vehicleId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var trip = Assert.Single(_trips.Trips);
        Assert.Equal(driverId, trip.DriverId);
        Assert.Equal("R. Ballantyne", trip.DriverName);
        Assert.Equal(vehicleId, trip.VehicleId);
        Assert.Equal("U-12", trip.VehicleUnit);
        // The fleet vehicle's capacity is server-authoritative — no manual figure exists.
        Assert.Equal(24, trip.SeatsCapacity);
    }

    private sealed class FakeTripNumberGenerator : ITripNumberGenerator
    {
        private int _next = 1000;

        public Task<string> NextAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult($"TR-{++_next}");
    }
}
