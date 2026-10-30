using NorthernLink.Trips.Application.Routes;
using NorthernLink.Trips.Application.Routes.Create;
using NorthernLink.Trips.Application.Routes.Update;
using NorthernLink.Trips.Application.Schedules.GenerateTrips;
using NorthernLink.Trips.Application.Trips;
using NorthernLink.Trips.Application.Trips.ChangeRoute;
using NorthernLink.Trips.Application.Trips.Create;
using NorthernLink.Trips.Application.Trips.Update;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Stops;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The Manitoba-only rule through the Application layer: the route handlers resolving a catalog
/// stop in Saskatchewan, the trip create/update handlers carrying the wizard's provinces, the
/// schedule materializer inheriting the route gate, and the public response shapes exposing
/// provinces for the wizard to grey things out with.
/// </summary>
public class OperatingAreaHandlerTests
{
    private readonly FakeStopRepository _stops = new();
    private readonly FakeRouteRepository _routes = new();
    private readonly FakeTripRepository _trips = new();
    private readonly FakeDriverLookupRepository _drivers = new();
    private readonly FakeVehicleLookupRepository _vehicles = new();

    public OperatingAreaHandlerTests()
    {
        _drivers.Drivers.Add(TestPlanning.ActiveDriver());
        _vehicles.Vehicles.Add(TestPlanning.ActiveVehicle());
    }

    /// <summary>Seeds an active catalog stop whose address province is whatever the dispatcher typed.</summary>
    private Stop SeedStop(string name, string province)
    {
        var stop = Stop.Create(
            TestPlanning.TenantId,
            name,
            StopType.Community,
            Address.Create(null, name, province, null, "Canada").Value,
            Coordinate.Create(55.0, -98.0).Value,
            notes: null).Value;
        _stops.Add(stop);
        return stop;
    }

    private static CreateRouteCommand RouteCommand(params Guid[] stopIds) => new(
        TestPlanning.TenantId,
        "Thompson ↔ Flin Flon",
        [.. stopIds.Select(id => new RouteStopInput(id))],
        DistanceKm: 390,
        EstimatedDurationMinutes: 300,
        RequiredLicenceClass: null);

    // ---- Routes ----

    [Fact]
    public async Task A_catalog_stop_in_Saskatchewan_may_exist_but_cannot_be_placed_on_a_new_route()
    {
        var thompson = SeedStop("Thompson", "Manitoba");
        var flinFlonSk = SeedStop("Flin Flon", "Saskatchewan");
        Assert.Equal("SK", flinFlonSk.Address.Province);

        var result = await new CreateRouteCommandHandler(_routes, _stops)
            .Handle(RouteCommand(thompson.Id, flinFlonSk.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Trips.Route.StopOutsideOperatingArea", result.Error.Code);
        Assert.Equal(
            "Stop \"Flin Flon\" is in Saskatchewan (SK), outside the Manitoba operating area, and cannot be placed on a route " + OperatingAreaTests.DeclineSuffix,
            result.Error.Message);
        Assert.Null(_routes.Added);
        Assert.Equal(0, _routes.SaveCount);
    }

    [Fact]
    public async Task A_catalog_stop_in_Saskatchewan_cannot_be_added_to_an_existing_route_either()
    {
        var thompson = SeedStop("Thompson", "MB");
        var lynnLake = SeedStop("Lynn Lake", "MB");
        var created = await new CreateRouteCommandHandler(_routes, _stops)
            .Handle(RouteCommand(thompson.Id, lynnLake.Id), CancellationToken.None);
        Assert.True(created.IsSuccess);
        var flinFlonSk = SeedStop("Flin Flon", "SK");

        var result = await new UpdateRouteCommandHandler(_routes, _stops).Handle(
            new UpdateRouteCommand(
                created.Value, "Thompson ↔ Flin Flon", [new RouteStopInput(thompson.Id), new RouteStopInput(flinFlonSk.Id)],
                390, 300, null, Active: true),
            CancellationToken.None);

        Assert.Equal("Trips.Route.StopOutsideOperatingArea", result.Error.Code);
        Assert.Equal(["Thompson", "Lynn Lake"], _routes.Added!.Stops.OrderBy(s => s.Order).Select(s => s.Name));
        Assert.Equal(1, _routes.SaveCount);
    }

    [Fact]
    public async Task The_resolver_copies_each_catalog_stop_province_onto_the_route_snapshot_as_a_code()
    {
        var thompson = SeedStop("Thompson", "Manitoba");
        var lynnLake = SeedStop("Lynn Lake", "mb");

        var result = await new CreateRouteCommandHandler(_routes, _stops)
            .Handle(RouteCommand(thompson.Id, lynnLake.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(_routes.Added!.Stops, stop => Assert.Equal("MB", stop.Province));
    }

    // ---- Trips ----

    private CreateTripCommandHandler CreateHandler => new(_trips, _routes, _drivers, _vehicles, new FakeTripNumberGenerator());

    private static CreateTripCommand FreeFormTrip(string destination, string? destinationProvince, IReadOnlyList<RouteStop>? stops = null) => new(
        TestPlanning.TenantId,
        new DateOnly(2026, 7, 21),
        new TimeOnly(6, 30),
        new TimeOnly(11, 30),
        TripServiceType.Charter,
        RouteId: null,
        RouteName: $"Thompson ↔ {destination}",
        Origin: "Thompson",
        Destination: destination,
        Stops: stops ?? [],
        DistanceKm: 390,
        Direction: null,
        IsEmptyLeg: false,
        ClientId: null,
        ClientName: null,
        PoNumber: null,
        DriverId: TestPlanning.DriverId,
        VehicleId: TestPlanning.VehicleId,
        SeatsMinimum: null,
        OriginProvince: null,
        DestinationProvince: destinationProvince);

    [Fact]
    public async Task A_free_form_trip_to_Flin_Flon_Saskatchewan_is_refused_and_nothing_is_saved()
    {
        var result = await CreateHandler.Handle(FreeFormTrip("Flin Flon", "SK"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Trips.Trip.OutsideOperatingArea", result.Error.Code);
        Assert.EndsWith(OperatingAreaTests.DeclineSuffix, result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_trips.Trips);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task A_free_form_trip_with_no_provinces_is_created_as_Manitoba()
    {
        var result = await CreateHandler.Handle(FreeFormTrip("Flin Flon", null), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Message);
        var trip = Assert.Single(_trips.Trips);
        Assert.Equal("MB", trip.OriginProvince);
        Assert.Equal("MB", trip.DestinationProvince);
    }

    [Fact]
    public async Task A_free_form_trip_with_a_stop_in_Saskatchewan_is_refused()
    {
        var stops = new List<RouteStop>
        {
            new() { Name = "Thompson", Order = 0 },
            new() { Name = "Creighton", Order = 1, Province = "Saskatchewan" },
            new() { Name = "Flin Flon", Order = 2, Province = "MB" },
        };

        var result = await CreateHandler.Handle(FreeFormTrip("Flin Flon", "MB", stops), CancellationToken.None);

        Assert.Equal("Trips.Trip.OutsideOperatingArea", result.Error.Code);
        Assert.Contains("stop \"Creighton\"", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_trips.Trips);
    }

    [Fact]
    public async Task A_route_backed_trip_ignores_the_free_form_provinces_and_takes_the_routes()
    {
        var route = TestPlanning.CreateRoute();
        _routes.Add(route);
        var command = FreeFormTrip("Flin Flon", "SK") with { RouteId = route.Id };

        var result = await CreateHandler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Message);
        var trip = Assert.Single(_trips.Trips);
        Assert.Equal("Lynn Lake", trip.Destination);
        Assert.Equal("MB", trip.OriginProvince);
        Assert.Equal("MB", trip.DestinationProvince);
        Assert.All(trip.Stops, stop => Assert.Equal("MB", stop.Province));
    }

    [Fact]
    public async Task Editing_a_free_form_trip_to_an_outside_destination_is_refused()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);

        var result = await new UpdateTripCommandHandler(_trips).Handle(
            new UpdateTripCommand(
                trip.Id, trip.ServiceDate, trip.WindowStart, trip.WindowEnd, trip.ServiceType, RouteId: null,
                trip.RouteName, trip.Origin, "Flin Flon", trip.Stops, 390, trip.ClientId, trip.ClientName, trip.PoNumber,
                trip.SeatsCapacity, trip.SeatsMinimum, OriginProvince: "MB", DestinationProvince: "SK"),
            CancellationToken.None);

        Assert.Equal("Trips.Trip.OutsideOperatingArea", result.Error.Code);
        Assert.Equal("Lynn Lake", trip.Destination);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Editing_a_free_form_trip_accepts_a_spelled_out_Manitoba()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);

        var result = await new UpdateTripCommandHandler(_trips).Handle(
            new UpdateTripCommand(
                trip.Id, trip.ServiceDate, trip.WindowStart, trip.WindowEnd, trip.ServiceType, RouteId: null,
                trip.RouteName, trip.Origin, "Flin Flon", trip.Stops, 390, trip.ClientId, trip.ClientName, trip.PoNumber,
                trip.SeatsCapacity, trip.SeatsMinimum, OriginProvince: "Manitoba", DestinationProvince: "man."),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Message);
        Assert.Equal("Flin Flon", trip.Destination);
        Assert.Equal("MB", trip.DestinationProvince);
        Assert.Equal(1, _trips.SaveCount);
    }

    // ---- Schedule generation ----

    [Fact]
    public async Task The_materializer_refuses_to_generate_from_a_legacy_route_with_a_stop_outside_Manitoba()
    {
        // A route written before the rule — Route.Create would refuse it today, so a template
        // pointing at one cannot materialize: the trip gate catches what the route gate predates.
        var legacy = OperatingAreaTests.LegacyCrossBorderRoute();
        _routes.Add(legacy);
        var templates = new FakeScheduleTemplateRepository();
        var template = TestPlanning.CreateTemplate(
            departureTime: new TimeOnly(6, 30),
            routeId: legacy.Id,
            defaultDriverId: TestPlanning.DriverId);
        templates.Add(template);
        var materializer = new ScheduleTripMaterializer(templates, _routes, _drivers, _vehicles, _trips, new FakeTripNumberGenerator());

        var plan = await materializer.PlanAsync(template.Id, TestPlanning.Monday, TestPlanning.Monday.AddDays(4), CancellationToken.None);
        Assert.True(plan.IsSuccess, plan.Error.Message);
        Assert.NotEmpty(plan.Value.Drafts);

        var result = await materializer.ApplyAsync(TestPlanning.TenantId, plan.Value, CancellationToken.None);

        Assert.Equal("Trips.Trip.OutsideOperatingArea", result.Error.Code);
        Assert.Empty(_trips.Trips);
        Assert.Equal(0, _trips.SaveCount);
    }

    // ---- Response shapes ----

    [Fact]
    public void The_route_change_preview_exposes_the_new_endpoint_and_stop_provinces()
    {
        var original = TestRouteChange.CreateRoute("Thompson", "Leaf Rapids", "Lynn Lake");
        var replacement = TestRouteChange.CreateRoute("Thompson", "Snow Lake", "Flin Flon");
        var trip = TestRouteChange.TripOn(original, TripDirection.Inbound);

        var leg = TripRouteChangeLegResponse.From(
            new TripRouteChangeLeg(trip, null, IsRequestedTrip: true, trip.RouteSnapshotFor(replacement), WillChange: true));

        Assert.Equal("MB", leg.NewOriginProvince);
        Assert.Equal("MB", leg.NewDestinationProvince);
        Assert.All(leg.NewStops!, stop => Assert.Equal("MB", stop.Province));
    }

    [Theory]
    [InlineData(typeof(TripResponse), "OriginProvince")]
    [InlineData(typeof(TripResponse), "DestinationProvince")]
    [InlineData(typeof(TripStopResponse), "Province")]
    [InlineData(typeof(RouteResponse), "OriginProvince")]
    [InlineData(typeof(RouteResponse), "DestinationProvince")]
    [InlineData(typeof(RouteStopResponse), "Province")]
    [InlineData(typeof(TripRouteChangeLegResponse), "NewOriginProvince")]
    [InlineData(typeof(TripRouteChangeLegResponse), "NewDestinationProvince")]
    public void The_public_contracts_carry_provinces_for_the_wizard(Type contract, string member)
    {
        // The wizard greys out routes with non-MB stops and pre-fills province selects from
        // these — the names are part of the API contract the Dispatcher reads.
        var property = contract.GetProperty(member);

        Assert.NotNull(property);
        Assert.Equal(typeof(string), property.PropertyType);
    }
}
