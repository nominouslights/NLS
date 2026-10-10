using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Trips;
using NorthernLink.Trips.Application.Trips.ChangeRoute;
using NorthernLink.Trips.Application.Trips.GetActivity;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Shipments;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Domain.Trips.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// <see cref="ChangeTripRouteCommandHandler"/> and <see cref="PreviewTripRouteChangeQueryHandler"/>
/// over the shared <see cref="TripRouteChangeImpactCalculator"/>. Also carries the re-routing
/// coverage that used to live in UpdateTripCommandHandlerTests, before PUT stopped re-routing.
/// </summary>
public class TripChangeRouteHandlerTests
{
    private readonly FakeTripRepository _trips = new();
    private readonly InMemoryRouteRepository _routes = new();
    private readonly FakeTripManifestRepository _manifests = new();
    private readonly FakeShipmentRepository _shipments = new();

    private readonly Route _original = TestRouteChange.CreateRoute("Thompson", "Leaf Rapids", "Lynn Lake");
    private readonly Route _replacement = TestRouteChange.CreateRoute(150, "Thompson", "Snow Lake", "Flin Flon");

    public TripChangeRouteHandlerTests()
    {
        _routes.Add(_original);
        _routes.Add(_replacement);
    }

    private TripRouteChangeImpactCalculator Calculator => new(_trips, _routes, _manifests, _shipments);

    private ChangeTripRouteCommandHandler Handler => new(Calculator, _trips);

    private PreviewTripRouteChangeQueryHandler Preview => new(Calculator);

    private Task<Shared.Kernel.Result> Change(Trip trip, Guid routeId, bool acknowledge = false) =>
        Handler.Handle(new ChangeTripRouteCommand(trip.Id, routeId, acknowledge), CancellationToken.None);

    private async Task<TripRouteChangePreviewResponse> PreviewOf(Trip trip, Guid routeId)
    {
        var result = await Preview.Handle(new PreviewTripRouteChangeQuery(trip.Id, routeId), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private Trip Add(Trip trip)
    {
        _trips.Add(trip);
        return trip;
    }

    private (Trip Outbound, Trip Inbound) AddPair()
    {
        var outbound = Add(TestRouteChange.TripOn(_original, TripDirection.Outbound, "TR-1001", roundTripKey: "tpl:pair"));
        var inbound = Add(TestRouteChange.TripOn(_original, TripDirection.Inbound, "TR-1002", roundTripKey: "tpl:pair"));
        return (outbound, inbound);
    }

    private TripManifest AddManifest(Trip trip, params ManifestPassenger[] passengers)
    {
        var manifest = TestRouteChange.ManifestFor(trip, passengers);
        _manifests.Add(manifest);
        return manifest;
    }

    private Shipment AddShipmentOn(Trip trip, string from, string to, string number = "SH-1001")
    {
        var shipment = TestShipments.Register(shipmentNumber: number);
        Assert.True(shipment.AddLeg(
            trip.Id, trip.TripNumber, trip.ServiceDate,
            TestRouteChange.StopId(from), from, TestRouteChange.StopId(to), to).IsSuccess);
        shipment.ClearDomainEvents();
        _shipments.Add(shipment);
        return shipment;
    }

    private static string[] Names(Trip trip) => [.. trip.Stops.OrderBy(s => s.Order).Select(s => s.Name)];

    // ---- Re-routing (moved from UpdateTripCommandHandlerTests) ----

    [Fact]
    public async Task An_unpaired_inbound_trip_takes_the_new_route_reversed()
    {
        var trip = Add(TestRouteChange.TripOn(_original, TripDirection.Inbound));

        var result = await Change(trip, _replacement.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(_replacement.Id, trip.RouteId);
        Assert.Equal("Flin Flon", trip.Origin);
        Assert.Equal("Thompson", trip.Destination);
        Assert.Equal(["Flin Flon", "Snow Lake", "Thompson"], Names(trip));
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Both_legs_change_in_one_save_each_oriented_for_its_own_direction()
    {
        var (outbound, inbound) = AddPair();

        var result = await Change(outbound, _replacement.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _trips.SaveCount);
        Assert.Equal(_replacement.Id, outbound.RouteId);
        Assert.Equal(_replacement.Id, inbound.RouteId);
        Assert.Equal(["Thompson", "Snow Lake", "Flin Flon"], Names(outbound));
        Assert.Equal(["Flin Flon", "Snow Lake", "Thompson"], Names(inbound));
        Assert.Equal(("Flin Flon", "Thompson"), (inbound.Origin, inbound.Destination));
        Assert.Equal(new TimeOnly(18, 30), inbound.WindowEnd); // 16:00 + 150 min
        Assert.IsType<TripRouteChangedDomainEvent>(Assert.Single(inbound.DomainEvents));
    }

    [Fact]
    public async Task Changing_from_the_return_leg_moves_the_outbound_leg_too()
    {
        var (outbound, inbound) = AddPair();

        Assert.True((await Change(inbound, _replacement.Id)).IsSuccess);

        Assert.Equal(["Thompson", "Snow Lake", "Flin Flon"], Names(outbound));
        Assert.Equal(["Flin Flon", "Snow Lake", "Thompson"], Names(inbound));
    }

    [Fact]
    public async Task Unknown_trip_is_not_found()
    {
        var result = await Handler.Handle(new ChangeTripRouteCommand(Guid.NewGuid(), _replacement.Id, true), CancellationToken.None);

        Assert.Equal(TripErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task A_missing_route_is_refused_without_saving()
    {
        var trip = Add(TestRouteChange.TripOn(_original, TripDirection.Inbound));

        var result = await Change(trip, Guid.NewGuid(), acknowledge: true);

        Assert.Equal(RouteErrors.NotFound, result.Error);
        Assert.Equal(_original.Id, trip.RouteId);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task An_inactive_route_is_refused()
    {
        var trip = Add(TestRouteChange.TripOn(_original, TripDirection.Outbound));
        Assert.True(_replacement.Update(
            _replacement.Name, _replacement.Stops, _replacement.DistanceKm, _replacement.EstimatedDuration, null, active: false).IsSuccess);

        var result = await Change(trip, _replacement.Id, acknowledge: true);

        Assert.Equal(TripErrors.RouteInactive, result.Error);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task The_current_route_is_refused_as_unchanged()
    {
        var trip = Add(TestRouteChange.TripOn(_original, TripDirection.Outbound));

        Assert.Equal(TripErrors.RouteUnchanged, (await Change(trip, _original.Id)).Error);
    }

    [Theory]
    [InlineData(TripStatus.InProgress)]
    [InlineData(TripStatus.ReadyForBilling)] // finished (the pair carries a client, so finishing lands here, not Completed)
    [InlineData(TripStatus.Cancelled)]
    public async Task A_partner_that_is_no_longer_scheduled_is_left_alone_and_only_the_requested_trip_changes(TripStatus partnerStatus)
    {
        var (outbound, inbound) = AddPair();
        MoveTo(outbound, partnerStatus);
        var outboundStopsBefore = Names(outbound);

        var refused = await Change(inbound, _replacement.Id);

        Assert.Equal(TripErrors.RouteChangeNeedsAcknowledgement, refused.Error);
        Assert.Equal(_original.Id, inbound.RouteId);
        Assert.Equal(0, _trips.SaveCount);

        var accepted = await Change(inbound, _replacement.Id, acknowledge: true);

        Assert.True(accepted.IsSuccess);
        Assert.Equal(1, _trips.SaveCount);
        Assert.Equal(_replacement.Id, inbound.RouteId);
        Assert.Equal(["Flin Flon", "Snow Lake", "Thompson"], Names(inbound));
        Assert.Equal(_original.Id, outbound.RouteId);
        Assert.Equal(partnerStatus, outbound.Status);
        Assert.Equal(outboundStopsBefore, Names(outbound));
        Assert.DoesNotContain(outbound.DomainEvents, e => e is TripRouteChangedDomainEvent);
    }

    [Theory]
    [InlineData(TripStatus.InProgress)]
    [InlineData(TripStatus.ReadyForBilling)] // finished (the pair carries a client, so finishing lands here, not Completed)
    [InlineData(TripStatus.Cancelled)]
    public async Task Preview_warns_that_a_partner_no_longer_scheduled_keeps_its_route(TripStatus partnerStatus)
    {
        var (outbound, inbound) = AddPair();
        MoveTo(outbound, partnerStatus);

        var preview = await PreviewOf(inbound, _replacement.Id);

        Assert.True(preview.CanChange);
        Assert.Empty(preview.Blockers);
        Assert.True(preview.RequiresAcknowledgement);
        var warning = Assert.Single(preview.Warnings);
        Assert.Equal(TripRouteChangeFindingCodes.PartnerNotChanged, warning.Code);
        Assert.Equal(("TR-1001", outbound.Id), (warning.TripNumber, warning.TripId));
        Assert.Null(warning.Count);
        Assert.Contains("TR-1001", warning.Message);
        Assert.Contains(partnerStatus.ToString(), warning.Message);
        Assert.Contains("no longer mirror", warning.Message);
        Assert.Equal([true, false], preview.Legs.Select(l => l.WillChange));
        Assert.Equal(partnerStatus.ToString(), preview.Partner!.Status);
    }

    [Fact]
    public async Task A_finished_partner_already_on_the_target_route_needs_no_warning()
    {
        var (outbound, inbound) = AddPair();
        Assert.True(outbound.ChangeRoute(_replacement).IsSuccess);
        MoveTo(outbound, TripStatus.ReadyForBilling);

        var preview = await PreviewOf(inbound, _replacement.Id);

        Assert.True(preview.CanChange);
        Assert.Empty(preview.Warnings);
        Assert.Equal([true, false], preview.Legs.Select(l => l.WillChange));
        Assert.True((await Change(inbound, _replacement.Id)).IsSuccess);
    }

    [Fact]
    public async Task Freight_underway_on_a_partner_that_is_left_alone_does_not_block()
    {
        var (outbound, inbound) = AddPair();
        var shipment = AddShipmentOn(outbound, "Thompson", "Lynn Lake");
        Assert.True(shipment.RecordLegPickup(1, DateTimeOffset.UtcNow, "driver").IsSuccess);
        Assert.True(outbound.Start().IsSuccess);

        var result = await Change(inbound, _replacement.Id, acknowledge: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(_replacement.Id, inbound.RouteId);
        Assert.Equal(_original.Id, outbound.RouteId);
    }

    [Fact]
    public async Task A_scheduled_booking_confirmed_partner_blocks_the_change()
    {
        var outbound = Add(TestRouteChange.TripOn(_original, TripDirection.Outbound, "TR-1001", roundTripKey: "tpl:pair"));
        var inbound = AddBookingPartner(outbound);

        var result = await Change(outbound, _replacement.Id, acknowledge: true);

        Assert.Equal("Trips.Trip.RouteOwnedByBooking", result.Error.Code);
        Assert.Contains("TR-3002", result.Error.Message);
        Assert.Equal(_original.Id, outbound.RouteId);
        Assert.Equal(_original.Id, inbound.RouteId);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Theory]
    [InlineData(TripStatus.Cancelled)]
    [InlineData(TripStatus.Completed)] // a booking run has no client, so finishing completes it
    public async Task A_booking_confirmed_partner_no_longer_scheduled_is_left_alone_rather_than_blocking(TripStatus partnerStatus)
    {
        var outbound = Add(TestRouteChange.TripOn(_original, TripDirection.Outbound, "TR-1001", roundTripKey: "tpl:pair"));
        var inbound = AddBookingPartner(outbound);
        MoveTo(inbound, partnerStatus);

        var preview = await PreviewOf(outbound, _replacement.Id);
        var result = await Change(outbound, _replacement.Id, acknowledge: true);

        Assert.Empty(preview.Blockers);
        Assert.Equal(TripRouteChangeFindingCodes.PartnerNotChanged, Assert.Single(preview.Warnings).Code);
        Assert.True(result.IsSuccess);
        Assert.Equal(_replacement.Id, outbound.RouteId);
        Assert.Equal(_original.Id, inbound.RouteId);
    }

    private Trip AddBookingPartner(Trip outbound)
    {
        var inbound = Trip.ScheduleFromBooking(
            TestPlanning.TenantId, "TR-3002", Guid.NewGuid(), outbound.ServiceDate, new TimeOnly(16, 0),
            _original.Id, _original.Name, _original.Destination, _original.Origin,
            RouteStop.OrientedFor(_original.Stops, TripDirection.Inbound), _original.DistanceKm,
            seatsConfirmed: 6, seatsCapacity: 12, seatsMinimum: 4).Value;
        Assert.True(inbound.AssignRoundTrip(outbound.RoundTripKey!, TripDirection.Inbound).IsSuccess);
        return Add(inbound);
    }

    private static void MoveTo(Trip trip, TripStatus status)
    {
        switch (status)
        {
            case TripStatus.InProgress:
                Assert.True(trip.Start().IsSuccess);
                break;
            case TripStatus.ReadyForBilling or TripStatus.Completed: // client trip → billing; no client → completed
                Assert.True(trip.Start().IsSuccess);
                Assert.True(trip.RecordPostTripInspection().IsSuccess);
                Assert.True(trip.FinishOperations().IsSuccess);
                break;
            case TripStatus.Cancelled:
                Assert.True(trip.Cancel("weather").IsSuccess);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        Assert.Equal(status, trip.Status);
    }

    [Fact]
    public async Task A_booking_confirmed_trip_is_refused()
    {
        var trip = Add(Trip.ScheduleFromBooking(
            TestPlanning.TenantId, "TR-3001", Guid.NewGuid(), new DateOnly(2026, 7, 21), new TimeOnly(8, 0),
            _original.Id, _original.Name, _original.Origin, _original.Destination, _original.Stops, _original.DistanceKm,
            seatsConfirmed: 6, seatsCapacity: 12, seatsMinimum: 4).Value);

        Assert.Equal(TripErrors.RouteOwnedByBooking, (await Change(trip, _replacement.Id, true)).Error);
    }

    [Fact]
    public async Task Freight_already_picked_up_on_the_partner_blocks_the_change()
    {
        var (outbound, inbound) = AddPair();
        var shipment = AddShipmentOn(inbound, "Lynn Lake", "Thompson");
        Assert.True(shipment.RecordLegPickup(1, DateTimeOffset.UtcNow, "driver").IsSuccess);

        var result = await Change(outbound, _replacement.Id, acknowledge: true);

        Assert.Equal("Trips.Trip.RouteChangeCargoUnderway", result.Error.Code);
        Assert.Contains("TR-1002", result.Error.Message);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Off_route_passengers_need_acknowledgement_then_succeed_and_are_left_as_recorded()
    {
        var trip = Add(TestRouteChange.TripOn(_original, TripDirection.Outbound));
        var manifest = AddManifest(trip,
            TestRouteChange.Passenger("On route", "Thompson", null),
            TestRouteChange.Passenger("Off route", "Thompson", "Leaf Rapids"));
        var passengersBefore = manifest.Passengers.ToList();

        var refused = await Change(trip, _replacement.Id);

        Assert.Equal(TripErrors.RouteChangeNeedsAcknowledgement, refused.Error);
        Assert.Equal(_original.Id, trip.RouteId);
        Assert.Equal(0, _trips.SaveCount);

        var accepted = await Change(trip, _replacement.Id, acknowledge: true);

        Assert.True(accepted.IsSuccess);
        Assert.Equal(_replacement.Id, trip.RouteId);
        Assert.Equal(passengersBefore, manifest.Passengers);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task The_manifest_route_name_follows_without_re_stamping_who_entered_it()
    {
        var (outbound, inbound) = AddPair();
        var outboundManifest = AddManifest(outbound, TestRouteChange.Passenger("A", "Thompson", null));
        var inboundManifest = AddManifest(inbound);
        var enteredAt = outboundManifest.EnteredAt;

        Assert.True((await Change(outbound, _replacement.Id)).IsSuccess);

        Assert.Equal(_replacement.Name, outboundManifest.Route);
        Assert.Equal(_replacement.Name, inboundManifest.Route);
        Assert.Equal("dispatch@northernlink.ca", outboundManifest.EnteredBy);
        Assert.Equal(enteredAt, outboundManifest.EnteredAt);
    }

    [Fact]
    public async Task A_bookeo_imported_trip_warns_and_needs_acknowledgement()
    {
        var trip = Add(Trip.ScheduleFromImport(
            TestPlanning.TenantId, "TR-5001", new DateOnly(2026, 7, 21), new TimeOnly(8, 0), null,
            _original.Id, _original.Name, _original.Origin, _original.Destination, _original.Stops, _original.DistanceKm,
            TripDirection.Outbound, seatsConfirmed: 3, vehicleId: null, vehicleUnit: null, vehicleSeatingCapacity: null).Value);

        var preview = await PreviewOf(trip, _replacement.Id);

        var warning = Assert.Single(preview.Warnings);
        Assert.Equal(TripRouteChangeFindingCodes.BookeoImported, warning.Code);
        Assert.True(preview.RequiresAcknowledgement);
        Assert.Equal(TripErrors.RouteChangeNeedsAcknowledgement, (await Change(trip, _replacement.Id)).Error);
        Assert.True((await Change(trip, _replacement.Id, acknowledge: true)).IsSuccess);
    }

    [Fact]
    public async Task A_schedule_generated_trip_gets_a_notice_that_needs_no_acknowledgement()
    {
        var trip = Add(TestRouteChange.TripOn(_original, TripDirection.Outbound, scheduleTemplateId: Guid.NewGuid()));

        var preview = await PreviewOf(trip, _replacement.Id);

        Assert.Equal(TripRouteChangeFindingCodes.ScheduleGenerated, Assert.Single(preview.Notices).Code);
        Assert.False(preview.RequiresAcknowledgement);
        Assert.True((await Change(trip, _replacement.Id)).IsSuccess);
    }

    [Fact]
    public async Task A_lost_concurrency_race_is_a_clean_conflict()
    {
        var trip = Add(TestRouteChange.TripOn(_original, TripDirection.Outbound));
        _trips.SimulateConcurrencyConflict = true;

        var result = await Change(trip, _replacement.Id);

        Assert.Equal(TripErrors.ChangedConcurrently, result.Error);
    }

    // ---- Preview ----

    [Fact]
    public async Task Preview_reports_the_partner_and_each_legs_new_corridor()
    {
        var (outbound, inbound) = AddPair();

        var preview = await PreviewOf(outbound, _replacement.Id);

        Assert.True(preview.CanChange);
        Assert.Empty(preview.Blockers);
        Assert.Equal(_replacement.Name, preview.NewRouteName);
        Assert.NotNull(preview.Partner);
        Assert.Equal(("TR-1002", "Scheduled", "Inbound"), (preview.Partner.TripNumber, preview.Partner.Status, preview.Partner.Direction));
        Assert.Equal(["TR-1001", "TR-1002"], preview.Legs.Select(l => l.TripNumber));
        var returnLeg = preview.Legs[1];
        Assert.False(returnLeg.IsRequestedTrip);
        Assert.Equal(("Flin Flon", "Thompson"), (returnLeg.NewOrigin, returnLeg.NewDestination));
        Assert.Equal(["Flin Flon", "Snow Lake", "Thompson"], returnLeg.NewStops!.Select(s => s.Name));
        Assert.Equal(new TimeOnly(17, 45), returnLeg.CurrentWindowEnd);
        Assert.Equal(new TimeOnly(18, 30), returnLeg.NewWindowEnd);
        // A preview persists nothing.
        Assert.Equal(_original.Id, inbound.RouteId);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Preview_lists_every_blocker_instead_of_failing()
    {
        var (outbound, inbound) = AddPair();
        Assert.True(outbound.Start().IsSuccess);
        var shipment = AddShipmentOn(inbound, "Lynn Lake", "Thompson");
        Assert.True(shipment.RecordLegPickup(1, DateTimeOffset.UtcNow, "driver").IsSuccess);

        var preview = await PreviewOf(outbound, Guid.NewGuid());

        Assert.False(preview.CanChange);
        Assert.Null(preview.NewRouteName);
        Assert.Equal(
            ["Trips.Trip.RouteChangeNotScheduled", "Trips.Route.NotFound", "Trips.Trip.RouteChangeCargoUnderway"],
            preview.Blockers.Select(b => b.Code));
        Assert.All(preview.Legs, leg => Assert.Null(leg.NewStops));
    }

    [Fact]
    public async Task Preview_counts_off_route_passengers_matching_by_stop_id_first_then_by_name()
    {
        var trip = Add(TestRouteChange.TripOn(_original, TripDirection.Outbound));
        AddManifest(trip,
            // Matched by id even though the name snapshot is stale.
            new ManifestPassenger { Name = "Renamed stop", PickupStopId = TestRouteChange.StopId("Thompson"), PickupStopName = "Thompson Depot" },
            // Id unknown to the catalogue, but the name matches a stop on the new route.
            new ManifestPassenger { Name = "Name fallback", DropoffStopId = Guid.NewGuid(), DropoffStopName = "Snow Lake" },
            // Free-text, no id, on the new route by name.
            new ManifestPassenger { Name = "Free text", PickupStopName = "Flin Flon" },
            // Nothing picked — nothing to orphan.
            new ManifestPassenger { Name = "Unassigned" },
            // Off-route by both id and name — counted once even though both ends are off.
            TestRouteChange.Passenger("Off both", "Leaf Rapids", "Lynn Lake"),
            // Name differs only by case — the editor compares exactly, so this is off-route.
            new ManifestPassenger { Name = "Case", PickupStopName = "snow lake" });

        var preview = await PreviewOf(trip, _replacement.Id);

        var warning = Assert.Single(preview.Warnings);
        Assert.Equal(TripRouteChangeFindingCodes.PassengerStopsOffRoute, warning.Code);
        Assert.Equal(2, warning.Count);
        Assert.Equal("TR-1001", warning.TripNumber);
    }

    [Fact]
    public async Task Preview_counts_planned_shipment_legs_off_the_new_route_but_ignores_cancelled_freight()
    {
        var trip = Add(TestRouteChange.TripOn(_original, TripDirection.Outbound));
        AddShipmentOn(trip, "Thompson", "Flin Flon", "SH-1");     // on route
        AddShipmentOn(trip, "Thompson", "Lynn Lake", "SH-2");     // off route
        var cancelled = AddShipmentOn(trip, "Leaf Rapids", "Lynn Lake", "SH-3");
        Assert.True(cancelled.Cancel("client cancelled").IsSuccess);

        var preview = await PreviewOf(trip, _replacement.Id);

        var warning = Assert.Single(preview.Warnings);
        Assert.Equal(TripRouteChangeFindingCodes.ShipmentStopsOffRoute, warning.Code);
        Assert.Equal(1, warning.Count);
    }

    [Fact]
    public async Task Preview_of_an_unknown_trip_is_not_found()
    {
        var result = await Preview.Handle(new PreviewTripRouteChangeQuery(Guid.NewGuid(), _replacement.Id), CancellationToken.None);

        Assert.Equal(TripErrors.NotFound, result.Error);
    }

    [Fact]
    public void Matcher_treats_an_empty_reference_as_unspecified_not_orphaned()
    {
        Assert.False(RouteStopMatcher.IsOrphaned(null, null, _replacement.Stops));
        Assert.False(RouteStopMatcher.IsOrphaned(null, string.Empty, _replacement.Stops));
        Assert.True(RouteStopMatcher.IsOrphaned(Guid.NewGuid(), null, _replacement.Stops));
    }

    // ---- Activity log ----

    [Fact]
    public async Task The_activity_timeline_shows_the_route_change_under_its_journal_name()
    {
        Assert.Equal("trip-route-changed", AuditNames.ForEvent(typeof(TripRouteChangedDomainEvent)));

        var trip = Add(TestRouteChange.TripOn(_original, TripDirection.Outbound));
        var activity = new FakeTripActivityReadService();
        activity.Entries.Add(new TripActivityJournalEntry(
            DateTimeOffset.UtcNow, "trip", AuditNames.ForEvent(typeof(TripRouteChangedDomainEvent)),
            $$"""{"tripId":"{{trip.Id}}","newRouteName":"{{_replacement.Name}}"}"""));

        var timeline = await new GetTripActivityQueryHandler(
                _trips, activity, new TripOperatorAccess(FakeCurrentActor.Dispatcher, new FakeDriverLookupRepository()))
            .Handle(new GetTripActivityQuery(trip.Id, TestPlanning.TenantId), CancellationToken.None);

        var entry = Assert.Single(timeline.Value);
        Assert.Equal("trip-route-changed", entry.EventType);
        Assert.Null(entry.EnteredBy);
    }
}
