using System.Reflection;
using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Planning;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Stops;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The Manitoba-only rule (<see cref="OperatingArea"/>) at the domain level: the hard-coded
/// area itself, then every corridor write that must refuse a stop or endpoint outside it —
/// route create/update, the three trip creation paths, trip edit, re-routing and the deadhead
/// return — and the message the dispatcher sees, pinned verbatim.
/// </summary>
public class OperatingAreaTests
{
    public const string DeclineSuffix = "— this request must be declined or referred to a partner carrier.";

    private static RouteStop StopIn(string name, string? province, int order) =>
        new() { Name = name, Order = order, Province = province };

    /// <summary>Thompson → Leaf Rapids → Lynn Lake with explicit "MB" codes on every stop.</summary>
    private static List<RouteStop> ManitobaStops() =>
    [
        StopIn("Thompson", "MB", 0),
        StopIn("Leaf Rapids", "MB", 1),
        StopIn("Lynn Lake", "MB", 2),
    ];

    /// <summary>Thompson → Flin Flon (SK): the second stop sits across the border.</summary>
    private static List<RouteStop> CrossBorderStops() =>
    [
        StopIn("Thompson", "MB", 0),
        StopIn("Flin Flon", "SK", 1),
    ];

    private static Result<Route> CreateRoute(IReadOnlyList<RouteStop> stops) =>
        Route.Create(TestPlanning.TenantId, "Test route", stops, 320, TimeSpan.FromMinutes(105), null);

    /// <summary>
    /// A route carrying a stop outside the area, as one written BEFORE the rule existed would —
    /// <see cref="Route.Create"/> refuses to build one, so the stops are swapped in behind the
    /// aggregate's back the way EF materialises a legacy row.
    /// </summary>
    public static Route LegacyCrossBorderRoute()
    {
        var route = CreateRoute(ManitobaStops()).Value;
        typeof(Route).GetProperty(nameof(Route.Stops))!.SetValue(route, CrossBorderStops());
        route.ClearDomainEvents();
        return route;
    }

    // ---- The area itself ----

    [Fact]
    public void The_operating_area_is_hard_coded_to_Manitoba_alone()
    {
        Assert.Equal(["MB"], OperatingArea.Provinces);
        Assert.Equal("Manitoba", OperatingArea.Description);
    }

    [Theory]
    [InlineData("MB", true)]
    [InlineData(null, true)]
    [InlineData("SK", false)]
    [InlineData("ON", false)]
    [InlineData("NU", false)]
    [InlineData("mb", false)]
    [InlineData("Manitoba", false)]
    public void Covers_compares_codes_and_treats_null_as_Manitoba(string? code, bool covered)
    {
        // Lower-case and full-name forms are NOT covered: Covers takes the stored code, and the
        // normalisers in front of it are what turn "Manitoba" into "MB".
        Assert.Equal(covered, OperatingArea.Covers(code));
    }

    // ---- Routes ----

    [Fact]
    public void A_route_with_a_stop_outside_Manitoba_cannot_be_created()
    {
        var result = CreateRoute(CrossBorderStops());

        Assert.True(result.IsFailure);
        Assert.Equal("Trips.Route.StopOutsideOperatingArea", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(
            "Stop \"Flin Flon\" is in Saskatchewan (SK), outside the Manitoba operating area, and cannot be placed on a route " + DeclineSuffix,
            result.Error.Message);
    }

    [Fact]
    public void A_route_cannot_be_updated_to_include_a_stop_outside_Manitoba()
    {
        var route = CreateRoute(ManitobaStops()).Value;
        route.ClearDomainEvents();

        var result = route.Update(route.Name, CrossBorderStops(), 300, TimeSpan.FromMinutes(90), null, active: true);

        Assert.True(result.IsFailure);
        Assert.Equal("Trips.Route.StopOutsideOperatingArea", result.Error.Code);
        Assert.Equal(["Thompson", "Leaf Rapids", "Lynn Lake"], route.Stops.OrderBy(s => s.Order).Select(s => s.Name));
        Assert.Equal(320, route.DistanceKm);
        Assert.Empty(route.DomainEvents);
    }

    [Fact]
    public void A_route_stop_with_an_unrecognised_province_is_refused_before_the_area_check()
    {
        var stops = new List<RouteStop> { StopIn("Thompson", "MB", 0), StopIn("Somewhere", "Minnesota", 1) };

        var result = CreateRoute(stops);

        Assert.Equal("Trips.Route.StopUnknownProvince", result.Error.Code);
        Assert.Contains("Somewhere", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("Minnesota", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Route_stops_with_no_province_are_Manitoba_and_are_stamped_as_such()
    {
        // The pre-rule shape: TestPlanning.Stops() carries no province at all.
        var route = CreateRoute(TestPlanning.Stops()).Value;

        Assert.All(route.Stops, stop => Assert.Equal("MB", stop.Province));
        Assert.Equal("MB", route.OriginProvince);
        Assert.Equal("MB", route.DestinationProvince);
    }

    [Fact]
    public void Route_stops_spelled_out_in_full_are_stored_as_codes()
    {
        var route = CreateRoute([StopIn("Thompson", "Manitoba", 0), StopIn("Lynn Lake", "man.", 1)]).Value;

        Assert.All(route.Stops, stop => Assert.Equal("MB", stop.Province));
    }

    [Fact]
    public void A_legacy_route_stop_with_null_province_reads_as_Manitoba()
    {
        var stop = new RouteStop { Name = "Thompson", Order = 0 };

        Assert.Null(stop.Province);
        Assert.Equal("MB", stop.EffectiveProvince);
        Assert.True(OperatingArea.ValidateRouteStops([stop]).IsSuccess);
    }

    // ---- Trips: free-form corridors ----

    [Fact]
    public void A_free_form_trip_to_Saskatchewan_is_refused_with_the_decline_message()
    {
        var result = TestPlanning.ScheduleTrip(destination: "Flin Flon", destinationProvince: "SK");

        Assert.True(result.IsFailure);
        Assert.Equal("Trips.Trip.OutsideOperatingArea", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(
            "The trip's destination \"Flin Flon\" is in Saskatchewan (SK), outside the Manitoba operating area " + DeclineSuffix,
            result.Error.Message);
    }

    [Theory]
    [InlineData("Saskatchewan")]
    [InlineData("sk")]
    [InlineData("Sask.")]
    public void Any_spelling_of_an_outside_province_is_refused(string typed)
    {
        var result = TestPlanning.ScheduleTrip(origin: "Flin Flon", originProvince: typed);

        Assert.Equal("Trips.Trip.OutsideOperatingArea", result.Error.Code);
        Assert.StartsWith("The trip's origin \"Flin Flon\" is in Saskatchewan (SK)", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_free_form_trip_with_an_unrecognised_province_is_refused_as_unknown()
    {
        var result = TestPlanning.ScheduleTrip(destination: "Grand Forks", destinationProvince: "North Dakota");

        Assert.Equal("Trips.Trip.UnknownProvince", result.Error.Code);
        Assert.Contains("North Dakota", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_free_form_trip_with_no_provinces_is_Manitoba_end_to_end()
    {
        var trip = TestPlanning.ScheduleTrip().Value;

        Assert.Equal("MB", trip.OriginProvince);
        Assert.Equal("MB", trip.DestinationProvince);
        Assert.All(trip.Stops, stop => Assert.Equal("MB", stop.Province));
    }

    [Fact]
    public void A_free_form_trip_with_a_stop_outside_Manitoba_is_refused_even_when_the_endpoints_are_inside()
    {
        var stops = new List<RouteStop>
        {
            StopIn("Thompson", null, 0),
            StopIn("Creighton", "SK", 1),
            StopIn("Flin Flon", "MB", 2),
        };

        var result = TestPlanning.ScheduleTrip(destination: "Flin Flon", stops: stops);

        Assert.Equal("Trips.Trip.OutsideOperatingArea", result.Error.Code);
        Assert.StartsWith("The trip's stop \"Creighton\" is in Saskatchewan (SK)", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Endpoint_provinces_left_blank_follow_the_first_and_last_stop()
    {
        // Explicit null endpoints, stops that say SK at the far end: the destination inherits it
        // and the trip is refused as a destination problem, not a stop problem.
        var result = TestPlanning.ScheduleTrip(destination: "Flin Flon", stops: CrossBorderStops());

        Assert.Equal("Trips.Trip.OutsideOperatingArea", result.Error.Code);
        Assert.StartsWith("The trip's destination \"Flin Flon\"", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Editing_a_free_form_trip_to_an_outside_destination_is_refused_and_changes_nothing()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.ClearDomainEvents();

        var result = trip.Update(
            trip.ServiceDate, trip.WindowStart, trip.WindowEnd, trip.ServiceType, trip.RouteId, trip.RouteName,
            trip.Origin, "Flin Flon", trip.Stops, trip.DistanceKm, trip.ClientId, trip.ClientName, trip.PoNumber,
            trip.SeatsCapacity, trip.SeatsMinimum, originProvince: null, destinationProvince: "SK");

        Assert.Equal("Trips.Trip.OutsideOperatingArea", result.Error.Code);
        Assert.Equal("Lynn Lake", trip.Destination);
        Assert.Equal("MB", trip.DestinationProvince);
        Assert.Empty(trip.DomainEvents);
    }

    // ---- Trips: snapshot paths ----

    [Fact]
    public void A_booking_confirmed_trip_refuses_a_corridor_with_a_stop_outside_Manitoba()
    {
        var result = Trip.ScheduleFromBooking(
            TestPlanning.TenantId, "TR-3001", Guid.NewGuid(), new DateOnly(2026, 7, 21), new TimeOnly(8, 0),
            Guid.NewGuid(), "Thompson ↔ Flin Flon", "Thompson", "Flin Flon", CrossBorderStops(), 390,
            seatsConfirmed: 6, seatsCapacity: 12, seatsMinimum: 4);

        Assert.Equal("Trips.Trip.OutsideOperatingArea", result.Error.Code);
    }

    [Fact]
    public void A_Bookeo_imported_trip_refuses_a_corridor_with_a_stop_outside_Manitoba()
    {
        var result = Trip.ScheduleFromImport(
            TestPlanning.TenantId, "TR-3002", new DateOnly(2026, 7, 21), new TimeOnly(8, 0), null,
            Guid.NewGuid(), "Thompson ↔ Flin Flon", "Thompson", "Flin Flon", CrossBorderStops(), 390,
            TripDirection.Outbound, seatsConfirmed: 3, vehicleId: null, vehicleUnit: null, vehicleSeatingCapacity: null);

        Assert.Equal("Trips.Trip.OutsideOperatingArea", result.Error.Code);
    }

    [Fact]
    public void A_route_backed_trip_takes_its_endpoint_provinces_from_the_route_stops()
    {
        var route = TestRouteChange.CreateRoute("Thompson", "Leaf Rapids", "Lynn Lake");
        var inbound = TestRouteChange.TripOn(route, TripDirection.Inbound);

        Assert.Equal("MB", inbound.OriginProvince);
        Assert.Equal("MB", inbound.DestinationProvince);
        Assert.All(inbound.Stops, stop => Assert.Equal("MB", stop.Province));
    }

    // ---- Re-routing ----

    [Fact]
    public void Changing_route_onto_a_legacy_route_with_a_stop_outside_Manitoba_is_refused()
    {
        var original = TestRouteChange.CreateRoute("Thompson", "Leaf Rapids", "Lynn Lake");
        var trip = TestRouteChange.TripOn(original, TripDirection.Outbound);
        var legacy = LegacyCrossBorderRoute();

        // The preview-side check still passes (the rule is about the corridor, not eligibility)…
        Assert.True(trip.CanChangeRouteTo(legacy).IsSuccess);

        // …and the change itself re-runs the gate on the new snapshot.
        var result = trip.ChangeRoute(legacy);

        Assert.Equal("Trips.Trip.OutsideOperatingArea", result.Error.Code);
        Assert.StartsWith("The trip's destination \"Flin Flon\" is in Saskatchewan (SK)", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(original.Id, trip.RouteId);
        Assert.Equal(["Thompson", "Leaf Rapids", "Lynn Lake"], trip.Stops.OrderBy(s => s.Order).Select(s => s.Name));
        Assert.Empty(trip.DomainEvents);
    }

    [Fact]
    public void The_route_snapshot_carries_the_endpoint_provinces_swapped_for_an_inbound_leg()
    {
        var route = TestRouteChange.CreateRoute("Thompson", "Lynn Lake");
        var inbound = TestRouteChange.TripOn(TestRouteChange.CreateRoute("Thompson", "Gillam"), TripDirection.Inbound);

        var snapshot = inbound.RouteSnapshotFor(route);

        Assert.Equal("Lynn Lake", snapshot.Origin);
        Assert.Equal("MB", snapshot.OriginProvince);
        Assert.Equal("Thompson", snapshot.Destination);
        Assert.Equal("MB", snapshot.DestinationProvince);
    }

    // ---- Deadhead return ----

    [Fact]
    public void A_deadhead_return_carries_the_source_provinces_swapped()
    {
        var source = TestPlanning.ScheduleTrip(clientId: Guid.NewGuid()).Value;

        var deadhead = source.DeadheadReturn("TR-1002").Value;

        Assert.Equal(source.DestinationProvince, deadhead.OriginProvince);
        Assert.Equal(source.OriginProvince, deadhead.DestinationProvince);
        Assert.All(deadhead.Stops, stop => Assert.Equal("MB", stop.Province));
    }

    // ---- The rule's guard rails ----

    [Fact]
    public void Route_create_cannot_be_talked_into_an_outside_stop_by_a_null_province_on_an_outside_name()
    {
        // The name is just a label; null province means Manitoba. This is the documented legacy
        // semantics, recorded so nobody "fixes" it into a geocoding guess.
        var result = CreateRoute([StopIn("Thompson", null, 0), StopIn("Flin Flon", null, 1)]);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value.Stops, stop => Assert.Equal("MB", stop.Province));
    }

    [Fact]
    public void The_reflection_helper_really_does_build_an_out_of_area_route()
    {
        // Guards the ChangeRoute test above against the helper silently building a valid route.
        var legacy = LegacyCrossBorderRoute();

        Assert.Equal("SK", legacy.DestinationProvince);
        Assert.Contains(legacy.Stops, stop => stop.Province == "SK");
        Assert.NotNull(typeof(Route).GetProperty(nameof(Route.Stops), BindingFlags.Public | BindingFlags.Instance));
    }
}
