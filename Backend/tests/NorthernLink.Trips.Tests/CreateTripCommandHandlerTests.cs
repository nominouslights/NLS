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

    private static CreateTripCommand Command(Guid? vehicleId = null, string? vehicleUnit = null) => new(
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
        DriverId: null,
        VehicleId: vehicleId,
        VehicleUnit: vehicleUnit,
        SeatsCapacity: 12,
        SeatsMinimum: null);

    [Fact]
    public async Task Unknown_vehicle_is_rejected()
    {
        var result = await Handler.Handle(Command(vehicleId: Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(TripErrors.VehicleNotFound, result.Error);
        Assert.Empty(_trips.Trips);
    }

    [Theory]
    [InlineData("InMaintenance")]
    [InlineData("OutOfService")]
    [InlineData("Retired")]
    public async Task Non_active_vehicle_is_rejected(string status)
    {
        var vehicleId = Guid.NewGuid();
        _vehicles.Vehicles.Add(Vehicle(vehicleId, status));

        var result = await Handler.Handle(Command(vehicleId: vehicleId), CancellationToken.None);

        Assert.Equal(TripErrors.VehicleNotActive, result.Error);
        Assert.Empty(_trips.Trips);
    }

    [Fact]
    public async Task Active_vehicle_is_snapshotted_onto_the_trip()
    {
        var vehicleId = Guid.NewGuid();
        _vehicles.Vehicles.Add(Vehicle(vehicleId));

        // A stale/free-form unit is ignored in favour of the lookup snapshot — and so is the
        // command's manual capacity (12): the vehicle's 24 seats are server-authoritative.
        var result = await Handler.Handle(
            Command(vehicleId: vehicleId, vehicleUnit: "stale-unit"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var trip = Assert.Single(_trips.Trips);
        Assert.Equal(vehicleId, trip.VehicleId);
        Assert.Equal("U-12", trip.VehicleUnit);
        Assert.Equal(24, trip.SeatsCapacity);
    }

    [Fact]
    public async Task No_vehicle_id_keeps_the_free_form_unit_and_leaves_vehicle_id_null()
    {
        var result = await Handler.Handle(Command(vehicleUnit: "U-99"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var trip = Assert.Single(_trips.Trips);
        Assert.Null(trip.VehicleId);
        Assert.Equal("U-99", trip.VehicleUnit);
        Assert.Equal(12, trip.SeatsCapacity); // no vehicle to derive from — manual capacity stands
    }

    private sealed class FakeTripNumberGenerator : ITripNumberGenerator
    {
        private int _next = 1000;

        public Task<string> NextAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult($"TR-{++_next}");
    }
}
