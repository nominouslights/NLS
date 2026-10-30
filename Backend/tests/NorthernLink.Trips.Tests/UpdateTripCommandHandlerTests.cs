using NorthernLink.Trips.Application.Trips.Update;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// Regression: the Dispatcher's edit form always re-sends the trip's current routeId, and the
/// handler used to re-snapshot the catalogue route in OUTBOUND order on every edit — so
/// changing just the PO on a return leg flipped its stops and origin/destination. Re-routing via
/// PUT is now refused (UseChangeRoute) — that coverage lives in TripChangeRouteHandlerTests.
/// </summary>
public class UpdateTripCommandHandlerTests
{
    private readonly FakeTripRepository _trips = new();
    private readonly FakeRouteRepository _routes = new();

    private UpdateTripCommandHandler Handler => new(_trips);

    private static Route CreateRoute(params string[] names) =>
        Route.Create(
            TestPlanning.TenantId,
            $"{names[0]} ↔ {names[^1]}",
            [.. names.Select((name, index) => new RouteStop
            {
                Name = name,
                Order = index,
                OutboundOffsetMinutes = index * 30,
                ReturnOffsetMinutes = (names.Length - 1 - index) * 40,
            })],
            distanceKm: 320,
            estimatedDuration: TimeSpan.FromMinutes(105),
            requiredLicenceClass: null).Value;

    /// <summary>A trip snapshotted from <paramref name="route"/> oriented for <paramref name="direction"/>, as generation builds it.</summary>
    private Trip AddTrip(Route route, TripDirection direction)
    {
        var inbound = direction == TripDirection.Inbound;
        var trip = Trip.Schedule(
            TestPlanning.TenantId,
            inbound ? "TR-1002" : "TR-1001",
            serviceDate: new DateOnly(2026, 7, 21),
            windowStart: inbound ? new TimeOnly(16, 0) : new TimeOnly(6, 30),
            windowEnd: null,
            TripServiceType.ContractCrew,
            routeId: route.Id,
            routeName: route.Name,
            origin: inbound ? route.Destination : route.Origin,
            destination: inbound ? route.Origin : route.Destination,
            stops: RouteStop.OrientedFor(route.Stops, direction),
            distanceKm: route.DistanceKm,
            scheduleTemplateId: null,
            roundTripKey: "tpl:test",
            direction: direction,
            isEmptyLeg: false,
            clientId: null,
            clientName: "Alamos Gold",
            poNumber: "PO-OLD",
            driverId: TestPlanning.DriverId,
            driverName: TestPlanning.DriverName,
            vehicleId: TestPlanning.VehicleId,
            vehicleUnit: TestPlanning.VehicleUnit,
            seatsCapacity: 12,
            seatsMinimum: null).Value;
        _trips.Add(trip);
        return trip;
    }

    /// <summary>What EditTripModal sends: the trip's own fields echoed back, plus the edit.</summary>
    private static UpdateTripCommand EditCommand(Trip trip, Guid? routeId, string poNumber) => new(
        trip.Id,
        trip.ServiceDate,
        trip.WindowStart,
        trip.WindowEnd,
        trip.ServiceType,
        routeId,
        trip.RouteName,
        trip.Origin,
        trip.Destination,
        trip.Stops,
        trip.DistanceKm,
        trip.ClientId,
        trip.ClientName,
        poNumber,
        trip.SeatsCapacity,
        trip.SeatsMinimum);

    private static string[] Names(Trip trip) => [.. trip.Stops.OrderBy(s => s.Order).Select(s => s.Name)];

    [Fact]
    public async Task Editing_the_PO_on_an_inbound_trip_keeps_its_reversed_stops_and_swapped_endpoints()
    {
        var route = CreateRoute("Thompson", "Leaf Rapids", "Lynn Lake");
        _routes.Add(route);
        var trip = AddTrip(route, TripDirection.Inbound);
        var before = trip.Stops.ToList();

        var result = await Handler.Handle(EditCommand(trip, route.Id, "PO-NEW"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("PO-NEW", trip.PoNumber);
        Assert.Equal("Lynn Lake", trip.Origin);
        Assert.Equal("Thompson", trip.Destination);
        Assert.Equal(["Lynn Lake", "Leaf Rapids", "Thompson"], Names(trip));
        Assert.Equal(before, trip.Stops); // offsets included, record equality
        Assert.Equal(route.Id, trip.RouteId);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Editing_the_PO_on_an_outbound_trip_leaves_its_route_unchanged()
    {
        var route = CreateRoute("Thompson", "Leaf Rapids", "Lynn Lake");
        _routes.Add(route);
        var trip = AddTrip(route, TripDirection.Outbound);
        var before = trip.Stops.ToList();

        var result = await Handler.Handle(EditCommand(trip, route.Id, "PO-NEW"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("PO-NEW", trip.PoNumber);
        Assert.Equal("Thompson", trip.Origin);
        Assert.Equal("Lynn Lake", trip.Destination);
        Assert.Equal(before, trip.Stops);
    }

    [Fact]
    public async Task An_unrelated_edit_does_not_pull_in_a_later_catalogue_route_edit()
    {
        var route = CreateRoute("Thompson", "Leaf Rapids", "Lynn Lake");
        _routes.Add(route);
        var trip = AddTrip(route, TripDirection.Outbound);
        var before = trip.Stops.ToList();

        // The catalogue route changes after the trip was created.
        Assert.True(route.Update(
            "Thompson ↔ Lynn Lake (via Gillam)",
            [
                new RouteStop { Name = "Thompson", Order = 0 },
                new RouteStop { Name = "Gillam", Order = 1 },
                new RouteStop { Name = "Lynn Lake", Order = 2 },
            ],
            distanceKm: 999,
            estimatedDuration: TimeSpan.FromMinutes(200),
            requiredLicenceClass: null,
            active: true).IsSuccess);

        var result = await Handler.Handle(EditCommand(trip, route.Id, "PO-NEW"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Thompson ↔ Lynn Lake", trip.RouteName);
        Assert.Equal(320, trip.DistanceKm);
        Assert.Equal(before, trip.Stops);
    }

    // Re-routing moved to ChangeTripRouteCommand (TripChangeRouteHandlerTests): it must move the
    // paired leg too, so PUT may only keep the trip's current route.

    [Fact]
    public async Task A_different_route_id_is_refused_with_UseChangeRoute_and_nothing_changes()
    {
        var original = CreateRoute("Thompson", "Leaf Rapids", "Lynn Lake");
        var trip = AddTrip(original, TripDirection.Inbound);
        var replacement = CreateRoute("Thompson", "Snow Lake", "Flin Flon");
        var before = trip.Stops.ToList();

        var result = await Handler.Handle(EditCommand(trip, replacement.Id, "PO-NEW"), CancellationToken.None);

        Assert.Equal(TripErrors.UseChangeRoute, result.Error);
        Assert.Equal(original.Id, trip.RouteId);
        Assert.Equal(before, trip.Stops);
        Assert.Equal("PO-OLD", trip.PoNumber);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Detaching_a_catalogue_trip_to_free_form_is_also_a_route_change()
    {
        var trip = AddTrip(CreateRoute("Thompson", "Lynn Lake"), TripDirection.Outbound);

        var result = await Handler.Handle(EditCommand(trip, routeId: null, "PO-NEW"), CancellationToken.None);

        Assert.Equal(TripErrors.UseChangeRoute, result.Error);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task A_free_form_trip_still_edits_its_corridor_text()
    {
        var trip = Trip.Schedule(
            TestPlanning.TenantId, "TR-2001", new DateOnly(2026, 7, 21), new TimeOnly(9, 0), null,
            TripServiceType.Charter, routeId: null, "Charter run", "Thompson", "Gillam", [], 280,
            scheduleTemplateId: null, roundTripKey: null, direction: null, isEmptyLeg: false,
            clientId: null, clientName: null, poNumber: null,
            TestPlanning.DriverId, TestPlanning.DriverName, TestPlanning.VehicleId, TestPlanning.VehicleUnit,
            seatsCapacity: null, seatsMinimum: null).Value;
        _trips.Add(trip);

        var command = EditCommand(trip, routeId: null, "PO-1") with { Destination = "Churchill", DistanceKm = 400 };
        var result = await Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(trip.RouteId);
        Assert.Equal("Churchill", trip.Destination);
        Assert.Equal(400, trip.DistanceKm);
    }

    [Fact]
    public async Task Attaching_a_catalogue_route_to_a_free_form_trip_goes_through_change_route()
    {
        var trip = Trip.Schedule(
            TestPlanning.TenantId, "TR-2002", new DateOnly(2026, 7, 21), new TimeOnly(9, 0), null,
            TripServiceType.Charter, routeId: null, "Charter run", "Thompson", "Gillam", [], 280,
            scheduleTemplateId: null, roundTripKey: null, direction: null, isEmptyLeg: false,
            clientId: null, clientName: null, poNumber: null,
            TestPlanning.DriverId, TestPlanning.DriverName, TestPlanning.VehicleId, TestPlanning.VehicleUnit,
            seatsCapacity: null, seatsMinimum: null).Value;
        _trips.Add(trip);

        var result = await Handler.Handle(EditCommand(trip, Guid.NewGuid(), "PO-1"), CancellationToken.None);

        Assert.Equal(TripErrors.UseChangeRoute, result.Error);
    }

    [Fact]
    public async Task An_edit_never_changes_the_deadhead_flag_either_way()
    {
        var route = CreateRoute("Thompson", "Leaf Rapids", "Lynn Lake");
        var passengerTrip = AddTrip(route, TripDirection.Outbound);
        var deadhead = AddTrip(route, TripDirection.Inbound);
        Assert.True(deadhead.ConvertToDeadhead([], 0, []).IsSuccess);

        // The command has no flag to carry any more — an edit leaves whatever the trip is.
        Assert.True((await Handler.Handle(EditCommand(passengerTrip, route.Id, "PO-NEW"), CancellationToken.None)).IsSuccess);
        Assert.True((await Handler.Handle(EditCommand(deadhead, route.Id, "PO-NEW"), CancellationToken.None)).IsSuccess);

        Assert.False(passengerTrip.IsEmptyLeg);
        Assert.True(deadhead.IsEmptyLeg);
        Assert.Equal("PO-NEW", deadhead.PoNumber);
        Assert.Null(typeof(UpdateTripCommand).GetProperty("IsEmptyLeg"));
    }
}
