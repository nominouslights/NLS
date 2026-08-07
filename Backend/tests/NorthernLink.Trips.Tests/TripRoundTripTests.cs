using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Domain.Trips.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// Dispatcher-made round trips: merging two existing client trips, unpairing, and the
/// deadhead-return factory. Template-generated pairing (creation-time key/direction) is
/// covered by <see cref="TripGeneratorTests"/>.
/// </summary>
public class TripRoundTripTests
{
    private static readonly Guid ClientId = Guid.NewGuid();

    private static Trip Outbound(
        Guid? clientId = null,
        TimeOnly? windowStart = null) =>
        TestPlanning.ScheduleTrip(
            tripNumber: "TR-2001",
            clientId: clientId ?? ClientId,
            windowStart: windowStart ?? new TimeOnly(6, 30)).Value;

    private static Trip Inbound(
        Guid? clientId = null,
        TimeOnly? windowStart = null,
        DateOnly? serviceDate = null,
        string origin = "Lynn Lake",
        string destination = "Thompson") =>
        TestPlanning.ScheduleTrip(
            tripNumber: "TR-2002",
            clientId: clientId ?? ClientId,
            serviceDate: serviceDate,
            windowStart: windowStart ?? new TimeOnly(16, 0),
            origin: origin,
            destination: destination).Value;

    // ---- Merge ----

    [Fact]
    public void Merge_pairs_both_legs_under_one_opaque_merge_key()
    {
        var outbound = Outbound();
        var inbound = Inbound();
        outbound.ClearDomainEvents();
        inbound.ClearDomainEvents();

        var result = Trip.MergeRoundTrip(outbound, inbound);

        Assert.True(result.IsSuccess);
        Assert.NotNull(outbound.RoundTripKey);
        Assert.StartsWith("merge:", outbound.RoundTripKey);
        Assert.Equal(outbound.RoundTripKey, inbound.RoundTripKey);
        Assert.Equal(TripDirection.Outbound, outbound.Direction);
        Assert.Equal(TripDirection.Inbound, inbound.Direction);
        Assert.Single(outbound.DomainEvents.OfType<TripRoundTripChangedDomainEvent>());
        Assert.Single(inbound.DomainEvents.OfType<TripRoundTripChangedDomainEvent>());
    }

    [Fact]
    public void Merge_assigns_outbound_to_the_earlier_departure_regardless_of_argument_order()
    {
        var morning = Outbound(windowStart: new TimeOnly(6, 30));
        var evening = Inbound(windowStart: new TimeOnly(16, 0));

        // Arguments reversed: the evening leg comes first but still becomes Inbound.
        var result = Trip.MergeRoundTrip(evening, morning);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripDirection.Outbound, morning.Direction);
        Assert.Equal(TripDirection.Inbound, evening.Direction);
    }

    [Fact]
    public void Merge_with_equal_departures_makes_the_first_argument_outbound()
    {
        var first = Outbound(windowStart: new TimeOnly(9, 0));
        var second = Inbound(windowStart: new TimeOnly(9, 0));

        var result = Trip.MergeRoundTrip(first, second);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripDirection.Outbound, first.Direction);
        Assert.Equal(TripDirection.Inbound, second.Direction);
    }

    [Fact]
    public void Merge_matches_corridors_trimmed_and_case_insensitively()
    {
        var outbound = Outbound();
        var inbound = Inbound(origin: "  LYNN LAKE ", destination: "thompson");

        Assert.True(Trip.MergeRoundTrip(outbound, inbound).IsSuccess);
    }

    [Fact]
    public void Merge_of_finished_trips_succeeds()
    {
        var outbound = Outbound();
        var inbound = Inbound();
        outbound.RecordPostTripInspection();
        outbound.FinishOperations();
        inbound.RecordPostTripInspection();
        inbound.FinishOperations();

        Assert.True(Trip.MergeRoundTrip(outbound, inbound).IsSuccess);
        Assert.Equal(outbound.RoundTripKey, inbound.RoundTripKey);
    }

    [Fact]
    public void Merge_with_itself_is_refused()
    {
        var trip = Outbound();

        var result = Trip.MergeRoundTrip(trip, trip);

        Assert.Equal(TripErrors.RoundTripSameTrip, result.Error);
    }

    [Fact]
    public void Merge_across_tenants_is_refused()
    {
        var outbound = Outbound();
        var foreign = TestPlanning.ScheduleTrip(
            tripNumber: "TR-2002",
            tenantId: Guid.NewGuid(),
            clientId: ClientId,
            windowStart: new TimeOnly(16, 0),
            origin: "Lynn Lake",
            destination: "Thompson").Value;

        Assert.Equal(TripErrors.RoundTripTenantMismatch, Trip.MergeRoundTrip(outbound, foreign).Error);
    }

    [Fact]
    public void Merge_without_a_client_on_either_leg_is_refused()
    {
        var withClient = Outbound();
        var communityRun = TestPlanning.ScheduleTrip(
            tripNumber: "TR-2002",
            clientId: null,
            windowStart: new TimeOnly(16, 0),
            origin: "Lynn Lake",
            destination: "Thompson").Value;

        Assert.Equal(TripErrors.RoundTripClientRequired, Trip.MergeRoundTrip(withClient, communityRun).Error);
        Assert.Equal(TripErrors.RoundTripClientRequired, Trip.MergeRoundTrip(communityRun, withClient).Error);
    }

    [Fact]
    public void Merge_across_clients_is_refused()
    {
        var outbound = Outbound();
        var otherClients = Inbound(clientId: Guid.NewGuid());

        Assert.Equal(TripErrors.RoundTripClientMismatch, Trip.MergeRoundTrip(outbound, otherClients).Error);
    }

    [Fact]
    public void Merge_across_service_dates_is_refused()
    {
        var outbound = Outbound();
        var nextDay = Inbound(serviceDate: new DateOnly(2026, 7, 22));

        Assert.Equal(TripErrors.RoundTripServiceDateMismatch, Trip.MergeRoundTrip(outbound, nextDay).Error);
    }

    [Fact]
    public void Merge_of_non_mirrored_corridors_is_refused()
    {
        var outbound = Outbound();
        var sameDirection = Inbound(origin: "Thompson", destination: "Lynn Lake");

        Assert.Equal(TripErrors.RoundTripCorridorMismatch, Trip.MergeRoundTrip(outbound, sameDirection).Error);
    }

    [Fact]
    public void Merge_with_a_cancelled_leg_is_refused()
    {
        var outbound = Outbound();
        var inbound = Inbound();
        inbound.Cancel("Weather");

        Assert.Equal(TripErrors.RoundTripFinal, Trip.MergeRoundTrip(outbound, inbound).Error);
    }

    [Fact]
    public void Merge_with_an_already_paired_leg_is_refused()
    {
        var outbound = Outbound();
        var inbound = Inbound();
        Assert.True(inbound.AssignRoundTrip("merge:existing", TripDirection.Inbound).IsSuccess);

        var result = Trip.MergeRoundTrip(outbound, inbound);

        Assert.Equal(TripErrors.RoundTripAlreadyPaired, result.Error);
        Assert.Null(outbound.RoundTripKey); // nothing half-applied
    }

    // ---- Merge with the manual override (allowMismatch) ----

    [Fact]
    public void Merge_with_override_pairs_across_service_dates_with_direction_by_date_order()
    {
        // Overnight return: departs the NEXT day, and earlier in the day than the outbound —
        // date order must win over time-of-day.
        var outbound = Outbound(windowStart: new TimeOnly(16, 0));
        var overnightReturn = Inbound(
            serviceDate: new DateOnly(2026, 7, 22), windowStart: new TimeOnly(6, 30));

        // Arguments reversed on purpose: the later-dated leg still becomes Inbound.
        var result = Trip.MergeRoundTrip(overnightReturn, outbound, allowMismatch: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripDirection.Outbound, outbound.Direction);
        Assert.Equal(TripDirection.Inbound, overnightReturn.Direction);
        Assert.NotNull(outbound.RoundTripKey);
        Assert.StartsWith("merge:", outbound.RoundTripKey);
        Assert.Equal(outbound.RoundTripKey, overnightReturn.RoundTripKey);
    }

    [Fact]
    public void Merge_with_override_pairs_non_mirrored_corridors()
    {
        var outbound = Outbound();
        var rewordedReturn = Inbound(origin: "Lynn Lake Townsite", destination: "Thompson Depot");

        Assert.True(Trip.MergeRoundTrip(outbound, rewordedReturn, allowMismatch: true).IsSuccess);
        Assert.Equal(outbound.RoundTripKey, rewordedReturn.RoundTripKey);
    }

    [Fact]
    public void Merge_with_override_still_refuses_merging_with_itself()
    {
        var trip = Outbound();

        Assert.Equal(TripErrors.RoundTripSameTrip, Trip.MergeRoundTrip(trip, trip, allowMismatch: true).Error);
    }

    [Fact]
    public void Merge_with_override_still_refuses_crossing_tenants()
    {
        var outbound = Outbound();
        var foreign = TestPlanning.ScheduleTrip(
            tripNumber: "TR-2002",
            tenantId: Guid.NewGuid(),
            clientId: ClientId,
            windowStart: new TimeOnly(16, 0),
            origin: "Lynn Lake",
            destination: "Thompson").Value;

        Assert.Equal(
            TripErrors.RoundTripTenantMismatch,
            Trip.MergeRoundTrip(outbound, foreign, allowMismatch: true).Error);
    }

    [Fact]
    public void Merge_with_override_still_requires_a_client_on_both_legs()
    {
        var withClient = Outbound();
        var communityRun = TestPlanning.ScheduleTrip(
            tripNumber: "TR-2002",
            clientId: null,
            windowStart: new TimeOnly(16, 0),
            origin: "Lynn Lake",
            destination: "Thompson").Value;

        Assert.Equal(
            TripErrors.RoundTripClientRequired,
            Trip.MergeRoundTrip(withClient, communityRun, allowMismatch: true).Error);
        Assert.Equal(
            TripErrors.RoundTripClientRequired,
            Trip.MergeRoundTrip(communityRun, withClient, allowMismatch: true).Error);
    }

    [Fact]
    public void Merge_with_override_still_refuses_crossing_clients()
    {
        var outbound = Outbound();
        var otherClients = Inbound(clientId: Guid.NewGuid());

        Assert.Equal(
            TripErrors.RoundTripClientMismatch,
            Trip.MergeRoundTrip(outbound, otherClients, allowMismatch: true).Error);
    }

    [Fact]
    public void Merge_with_override_still_refuses_a_cancelled_leg()
    {
        var outbound = Outbound();
        var inbound = Inbound();
        inbound.Cancel("Weather");

        Assert.Equal(
            TripErrors.RoundTripFinal,
            Trip.MergeRoundTrip(outbound, inbound, allowMismatch: true).Error);
    }

    [Fact]
    public void Merge_with_override_still_refuses_an_already_paired_leg()
    {
        var outbound = Outbound();
        var inbound = Inbound();
        Assert.True(inbound.AssignRoundTrip("merge:existing", TripDirection.Inbound).IsSuccess);

        var result = Trip.MergeRoundTrip(outbound, inbound, allowMismatch: true);

        Assert.Equal(TripErrors.RoundTripAlreadyPaired, result.Error);
        Assert.Null(outbound.RoundTripKey); // nothing half-applied
    }

    // ---- Unpair ----

    [Fact]
    public void Clear_round_trip_clears_key_and_direction_and_raises_the_changed_event()
    {
        var outbound = Outbound();
        var inbound = Inbound();
        Trip.MergeRoundTrip(outbound, inbound);
        outbound.ClearDomainEvents();

        var result = outbound.ClearRoundTrip();

        Assert.True(result.IsSuccess);
        Assert.Null(outbound.RoundTripKey);
        Assert.Null(outbound.Direction);
        Assert.Single(outbound.DomainEvents.OfType<TripRoundTripChangedDomainEvent>());
    }

    [Fact]
    public void Clear_round_trip_on_an_unpaired_trip_is_refused()
    {
        var trip = Outbound();

        Assert.Equal(TripErrors.RoundTripNotPaired, trip.ClearRoundTrip().Error);
    }

    // ---- Deadhead return ----

    [Fact]
    public void Deadhead_return_creates_the_reversed_empty_leg_and_pairs_the_source_as_outbound()
    {
        var source = Outbound();
        source.ClearDomainEvents();

        var result = source.CreateDeadheadReturn("TR-3001");

        Assert.True(result.IsSuccess);
        var deadhead = result.Value;

        // Reversed corridor + stops (renumbered from 0).
        Assert.Equal(source.Destination, deadhead.Origin);
        Assert.Equal(source.Origin, deadhead.Destination);
        Assert.Equal(
            source.Stops.OrderByDescending(s => s.Order).Select(s => s.Name),
            deadhead.Stops.OrderBy(s => s.Order).Select(s => s.Name));
        Assert.Equal([0, 1, 2], deadhead.Stops.Select(s => s.Order));

        // Same when/what/who-for.
        Assert.Equal(source.ServiceDate, deadhead.ServiceDate);
        Assert.Equal(source.WindowEnd, (TimeOnly?)deadhead.WindowStart); // departs when the source arrives
        Assert.Null(deadhead.WindowEnd);
        Assert.Equal(source.DistanceKm, deadhead.DistanceKm);
        Assert.Equal(source.ServiceType, deadhead.ServiceType);
        Assert.Equal(source.ClientId, deadhead.ClientId);
        Assert.Equal(source.ClientName, deadhead.ClientName);
        Assert.Equal(source.PoNumber, deadhead.PoNumber);
        Assert.Equal("TR-3001", deadhead.TripNumber);

        // Empty repositioning leg: nobody assigned, nothing sold.
        Assert.True(deadhead.IsEmptyLeg);
        Assert.Null(deadhead.DriverId);
        Assert.Null(deadhead.VehicleId);
        Assert.Null(deadhead.VehicleUnit);
        Assert.Null(deadhead.SeatsCapacity);
        Assert.Equal(TripStatus.Scheduled, deadhead.Status);
        Assert.Null(deadhead.ScheduleTemplateId);

        // Shared fresh pair.
        Assert.NotNull(deadhead.RoundTripKey);
        Assert.StartsWith("merge:", deadhead.RoundTripKey);
        Assert.Equal(deadhead.RoundTripKey, source.RoundTripKey);
        Assert.Equal(TripDirection.Inbound, deadhead.Direction);
        Assert.Equal(TripDirection.Outbound, source.Direction);
        Assert.Single(source.DomainEvents.OfType<TripRoundTripChangedDomainEvent>());
    }

    [Fact]
    public void Deadhead_return_of_an_open_ended_source_departs_at_the_source_window_start()
    {
        var source = TestPlanning.ScheduleTrip(clientId: ClientId, windowStart: new TimeOnly(6, 30)).Value;
        // The helper always sets a window end; open-ended trips clear it through Update.
        Assert.True(source.Update(
            source.ServiceDate, source.WindowStart, null, source.ServiceType,
            source.RouteId, source.RouteName, source.Origin, source.Destination, source.Stops,
            source.DistanceKm, source.IsEmptyLeg, source.ClientId, source.ClientName,
            source.PoNumber, source.SeatsCapacity, source.SeatsMinimum).IsSuccess);
        Assert.Null(source.WindowEnd);

        var deadhead = source.CreateDeadheadReturn("TR-3001").Value;

        Assert.Equal(new TimeOnly(6, 30), deadhead.WindowStart);
    }

    [Fact]
    public void Deadhead_return_requires_a_client()
    {
        var communityRun = TestPlanning.ScheduleTrip(clientId: null).Value;

        Assert.Equal(TripErrors.RoundTripClientRequired, communityRun.CreateDeadheadReturn("TR-3001").Error);
    }

    [Fact]
    public void Deadhead_return_of_an_already_paired_trip_is_refused()
    {
        var source = Outbound();
        source.AssignRoundTrip("merge:existing", TripDirection.Outbound);

        Assert.Equal(TripErrors.RoundTripAlreadyPaired, source.CreateDeadheadReturn("TR-3001").Error);
    }

    [Fact]
    public void Deadhead_return_of_a_cancelled_trip_is_refused()
    {
        var source = Outbound();
        source.Cancel(null);

        Assert.Equal(TripErrors.RoundTripFinal, source.CreateDeadheadReturn("TR-3001").Error);
    }

    [Fact]
    public void Deadhead_return_of_an_empty_leg_is_refused()
    {
        var emptyLeg = TestPlanning.ScheduleTrip(clientId: ClientId, isEmptyLeg: true).Value;

        Assert.Equal(TripErrors.DeadheadReturnOfEmptyLeg, emptyLeg.CreateDeadheadReturn("TR-3001").Error);
        Assert.Null(emptyLeg.RoundTripKey);
    }
}
