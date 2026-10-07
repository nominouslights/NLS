using NorthernLink.Trips.Application.Manifests;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Manifests.Events;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Domain.Trips.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>Domain rules of <see cref="Trip.ChangeRoute"/> and its companions.</summary>
public class TripChangeRouteTests
{
    private static readonly Route Original = TestRouteChange.CreateRoute("Thompson", "Leaf Rapids", "Lynn Lake");

    private static string[] Names(Trip trip) => [.. trip.Stops.OrderBy(s => s.Order).Select(s => s.Name)];

    [Fact]
    public void Outbound_trip_takes_the_new_route_in_outbound_order_with_its_distance_and_duration()
    {
        var trip = TestRouteChange.TripOn(Original, TripDirection.Outbound);
        var replacement = TestRouteChange.CreateRoute(150, "Thompson", "Snow Lake", "Flin Flon");

        var result = trip.ChangeRoute(replacement);

        Assert.True(result.IsSuccess);
        Assert.Equal(replacement.Id, trip.RouteId);
        Assert.Equal(replacement.Name, trip.RouteName);
        Assert.Equal("Thompson", trip.Origin);
        Assert.Equal("Flin Flon", trip.Destination);
        Assert.Equal(["Thompson", "Snow Lake", "Flin Flon"], Names(trip));
        Assert.Equal(replacement.DistanceKm, trip.DistanceKm);
        Assert.Equal(new TimeOnly(9, 0), trip.WindowEnd); // 06:30 + 150 min
    }

    [Fact]
    public void Inbound_trip_gets_reversed_stops_and_swapped_endpoints()
    {
        var trip = TestRouteChange.TripOn(Original, TripDirection.Inbound);
        var replacement = TestRouteChange.CreateRoute("Thompson", "Snow Lake", "Flin Flon");

        Assert.True(trip.ChangeRoute(replacement).IsSuccess);

        Assert.Equal("Flin Flon", trip.Origin);
        Assert.Equal("Thompson", trip.Destination);
        Assert.Equal(["Flin Flon", "Snow Lake", "Thompson"], Names(trip));
        // Offsets stay attached to their own stop; Inbound selects the return one (0 first).
        Assert.Equal([0, 40, 80], trip.Stops.OrderBy(s => s.Order).Select(s => s.ReturnOffsetMinutes!.Value));
        Assert.Equal(new TimeOnly(17, 45), trip.WindowEnd); // 16:00 + 105 min
    }

    [Fact]
    public void An_open_ended_window_stays_open_ended()
    {
        var trip = TestRouteChange.TripOn(Original, TripDirection.Outbound, openEnded: true);

        Assert.True(trip.ChangeRoute(TestRouteChange.CreateRoute("Thompson", "Gillam")).IsSuccess);

        Assert.Null(trip.WindowEnd);
    }

    [Fact]
    public void Everything_but_the_route_snapshot_and_window_end_is_left_alone()
    {
        var templateId = Guid.NewGuid();
        var trip = TestRouteChange.TripOn(
            Original, TripDirection.Inbound, tripNumber: "TR-4242", roundTripKey: "tpl:abc", scheduleTemplateId: templateId);
        var before = (trip.TripNumber, trip.ServiceDate, trip.WindowStart, trip.ServiceType, trip.ScheduleTemplateId,
            trip.RoundTripKey, trip.Direction, trip.IsEmptyLeg, trip.ClientId, trip.ClientName, trip.PoNumber,
            trip.DriverId, trip.DriverName, trip.VehicleId, trip.VehicleUnit, trip.SeatsCapacity, trip.SeatsConfirmed,
            trip.SeatsMinimum, trip.Status, trip.ManifestId);

        Assert.True(trip.ChangeRoute(TestRouteChange.CreateRoute("Thompson", "Gillam")).IsSuccess);

        Assert.Equal(before, (trip.TripNumber, trip.ServiceDate, trip.WindowStart, trip.ServiceType, trip.ScheduleTemplateId,
            trip.RoundTripKey, trip.Direction, trip.IsEmptyLeg, trip.ClientId, trip.ClientName, trip.PoNumber,
            trip.DriverId, trip.DriverName, trip.VehicleId, trip.VehicleUnit, trip.SeatsCapacity, trip.SeatsConfirmed,
            trip.SeatsMinimum, trip.Status, trip.ManifestId));
    }

    [Fact]
    public void Raises_the_route_changed_event_with_before_and_after()
    {
        var trip = TestRouteChange.TripOn(Original, TripDirection.Outbound);
        var replacement = TestRouteChange.CreateRoute("Thompson", "Gillam");

        trip.ChangeRoute(replacement);

        var raised = Assert.IsType<TripRouteChangedDomainEvent>(Assert.Single(trip.DomainEvents));
        Assert.Equal(trip.Id, raised.TripId);
        Assert.Equal(Original.Id, raised.PreviousRouteId);
        Assert.Equal(Original.Name, raised.PreviousRouteName);
        Assert.Equal(replacement.Id, raised.NewRouteId);
        Assert.Equal(replacement.Name, raised.NewRouteName);
    }

    [Fact]
    public void A_started_trip_cannot_change_route()
    {
        var trip = TestRouteChange.TripOn(Original, TripDirection.Outbound);
        Assert.True(trip.Start().IsSuccess);

        var result = trip.ChangeRoute(TestRouteChange.CreateRoute("Thompson", "Gillam"));

        Assert.Equal(TripErrors.RouteChangeNotScheduled, result.Error);
        Assert.Equal(Original.Id, trip.RouteId);
    }

    [Fact]
    public void A_cancelled_trip_cannot_change_route()
    {
        var trip = TestRouteChange.TripOn(Original, TripDirection.Outbound);
        Assert.True(trip.Cancel("weather").IsSuccess);

        var result = trip.ChangeRoute(TestRouteChange.CreateRoute("Thompson", "Gillam"));

        Assert.Equal(TripErrors.RouteChangeNotScheduled, result.Error);
    }

    [Fact]
    public void A_booking_confirmed_trip_cannot_change_route()
    {
        var trip = Trip.ScheduleFromBooking(
            TestPlanning.TenantId, "TR-3001", Guid.NewGuid(), new DateOnly(2026, 7, 21), new TimeOnly(8, 0),
            Original.Id, Original.Name, Original.Origin, Original.Destination, Original.Stops, Original.DistanceKm,
            seatsConfirmed: 6, seatsCapacity: 12, seatsMinimum: 4).Value;

        var result = trip.ChangeRoute(TestRouteChange.CreateRoute("Thompson", "Gillam"));

        Assert.Equal(TripErrors.RouteOwnedByBooking, result.Error);
        Assert.Equal(Original.Id, trip.RouteId);
    }

    [Fact]
    public void The_current_route_is_refused_as_unchanged()
    {
        var trip = TestRouteChange.TripOn(Original, TripDirection.Outbound);

        Assert.Equal(TripErrors.RouteUnchanged, trip.ChangeRoute(Original).Error);
        Assert.Empty(trip.DomainEvents);
    }

    [Fact]
    public void An_inactive_route_is_refused()
    {
        var trip = TestRouteChange.TripOn(Original, TripDirection.Outbound);
        var retired = TestRouteChange.CreateRoute("Thompson", "Gillam");
        Assert.True(retired.Update(retired.Name, retired.Stops, retired.DistanceKm, retired.EstimatedDuration, null, active: false).IsSuccess);

        Assert.Equal(TripErrors.RouteInactive, trip.ChangeRoute(retired).Error);
    }

    [Fact]
    public void Update_refuses_a_different_route_id()
    {
        var trip = TestRouteChange.TripOn(Original, TripDirection.Outbound);
        var other = TestRouteChange.CreateRoute("Thompson", "Gillam");

        var result = trip.Update(
            trip.ServiceDate, trip.WindowStart, trip.WindowEnd, trip.ServiceType,
            other.Id, other.Name, other.Origin, other.Destination, other.Stops, other.DistanceKm,
            trip.IsEmptyLeg, trip.ClientId, trip.ClientName, trip.PoNumber, trip.SeatsCapacity, trip.SeatsMinimum);

        Assert.Equal(TripErrors.UseChangeRoute, result.Error);
        Assert.Equal(Original.Id, trip.RouteId);
    }

    [Fact]
    public void Manifest_rename_touches_only_the_route_name_and_keeps_provenance()
    {
        var trip = TestRouteChange.TripOn(Original, TripDirection.Outbound);
        var manifest = TestRouteChange.ManifestFor(trip, TestRouteChange.Passenger("A. Moose", "Thompson", "Lynn Lake"));
        var (source, enteredBy, enteredAt, passengers) = (manifest.Source, manifest.EnteredBy, manifest.EnteredAt, manifest.Passengers.ToList());

        Assert.True(manifest.RenameRoute("Thompson ↔ Gillam").IsSuccess);

        Assert.Equal("Thompson ↔ Gillam", manifest.Route);
        Assert.Equal(source, manifest.Source);
        Assert.Equal(enteredBy, manifest.EnteredBy);
        Assert.Equal(enteredAt, manifest.EnteredAt);
        Assert.Equal(passengers, manifest.Passengers);
        var raised = Assert.IsType<TripManifestRouteRenamedDomainEvent>(Assert.Single(manifest.DomainEvents));
        Assert.Equal(Original.Name, raised.PreviousRoute);
    }

    [Fact]
    public void Manifest_rename_to_the_same_name_is_a_silent_no_op()
    {
        var trip = TestRouteChange.TripOn(Original, TripDirection.Outbound);
        var manifest = TestRouteChange.ManifestFor(trip);

        Assert.True(manifest.RenameRoute(Original.Name).IsSuccess);
        Assert.Empty(manifest.DomainEvents);
    }

    [Fact]
    public void Route_change_events_stay_internal()
    {
        var mapper = new TripsIntegrationEventMapper();
        var trip = TestRouteChange.TripOn(Original, TripDirection.Outbound);
        var manifest = TestRouteChange.ManifestFor(trip);
        trip.ChangeRoute(TestRouteChange.CreateRoute("Thompson", "Gillam"));
        manifest.RenameRoute("Thompson ↔ Gillam");

        Assert.Null(mapper.Map(trip.DomainEvents.Single(), trip));
        Assert.Null(mapper.Map(manifest.DomainEvents.Single(), manifest));
    }
}
