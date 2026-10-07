using NorthernLink.Trips.Application.Trips.Update;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// Regression: the Dispatcher's edit form always re-sends the trip's current routeId, and the
/// handler used to re-snapshot the catalogue route in OUTBOUND order on every edit — so
/// changing just the PO on a return leg flipped its stops and origin/destination.
/// </summary>
public class UpdateTripCommandHandlerTests
{
    private readonly FakeTripRepository _trips = new();
    private readonly FakeRouteRepository _routes = new();

    private UpdateTripCommandHandler Handler => new(_trips, _routes);

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
        trip.IsEmptyLeg,
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

    [Fact]
    public async Task Rerouting_an_inbound_trip_snapshots_the_new_route_reversed()
    {
        var original = CreateRoute("Thompson", "Leaf Rapids", "Lynn Lake");
        var trip = AddTrip(original, TripDirection.Inbound);
        var replacement = CreateRoute("Thompson", "Snow Lake", "Flin Flon");
        _routes.Add(replacement);

        var result = await Handler.Handle(EditCommand(trip, replacement.Id, "PO-OLD"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(replacement.Id, trip.RouteId);
        Assert.Equal(replacement.Name, trip.RouteName);
        Assert.Equal("Flin Flon", trip.Origin);
        Assert.Equal("Thompson", trip.Destination);
        Assert.Equal(["Flin Flon", "Snow Lake", "Thompson"], Names(trip));
        // Offsets stay attached to their own stop; Inbound selects the return one (0 first).
        Assert.Equal([0, 40, 80], trip.Stops.OrderBy(s => s.Order).Select(s => s.ReturnOffsetMinutes!.Value));
        Assert.Equal([60, 30, 0], trip.Stops.OrderBy(s => s.Order).Select(s => s.OutboundOffsetMinutes!.Value));
    }

    [Fact]
    public async Task Rerouting_an_outbound_trip_snapshots_the_new_route_in_outbound_order()
    {
        var original = CreateRoute("Thompson", "Leaf Rapids", "Lynn Lake");
        var trip = AddTrip(original, TripDirection.Outbound);
        var replacement = CreateRoute("Thompson", "Snow Lake", "Flin Flon");
        _routes.Add(replacement);

        var result = await Handler.Handle(EditCommand(trip, replacement.Id, "PO-OLD"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Thompson", trip.Origin);
        Assert.Equal("Flin Flon", trip.Destination);
        Assert.Equal(["Thompson", "Snow Lake", "Flin Flon"], Names(trip));
    }

    [Fact]
    public async Task Rerouting_to_an_unknown_route_fails_without_saving()
    {
        var trip = AddTrip(CreateRoute("Thompson", "Lynn Lake"), TripDirection.Inbound);

        var result = await Handler.Handle(EditCommand(trip, Guid.NewGuid(), "PO-NEW"), CancellationToken.None);

        Assert.Equal(RouteErrors.NotFound, result.Error);
        Assert.Equal(0, _trips.SaveCount);
    }
}
