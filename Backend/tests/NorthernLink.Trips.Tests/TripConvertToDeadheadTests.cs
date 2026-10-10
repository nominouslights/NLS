using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Application.Trips;
using NorthernLink.Trips.Application.Trips.ChangeStatus;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Domain.Trips.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The aggregate half of converting a trip to a deadhead and back: one test per refusal rule,
/// what success changes (and what it leaves alone), and the start/finish gates coming back once
/// a deadhead is converted back.
/// </summary>
public class TripConvertToDeadheadTests
{
    private static readonly Guid ClientId = Guid.Parse("00000000-0000-0000-0000-0000000000c1");

    private static Trip ScheduledTrip(
        string tripNumber = "TR-1001",
        string? roundTripKey = null,
        bool isEmptyLeg = false,
        TripServiceType serviceType = TripServiceType.ContractCrew)
    {
        var trip = TestPlanning.ScheduleTrip(
            tripNumber: tripNumber,
            roundTripKey: roundTripKey,
            isEmptyLeg: isEmptyLeg,
            clientId: ClientId,
            serviceType: serviceType).Value;
        trip.ClearDomainEvents();
        return trip;
    }

    private static TripManifest EmptyManifest(string tripNumber = "TR-1001") =>
        TestManifests.Create(tripNumber: tripNumber, passengers: []).Value;

    private static Result Convert(Trip trip, IReadOnlyCollection<TripManifest>? manifests = null, int bookings = 0, IReadOnlyCollection<Trip>? partners = null) =>
        trip.ConvertToDeadhead(manifests ?? [], bookings, partners ?? []);

    // ------------------------------------------------------------------ success

    [Fact]
    public void Converting_an_empty_scheduled_trip_sets_the_flag_clears_the_manifest_link_and_raises_the_event()
    {
        var trip = ScheduledTrip();
        var linked = EmptyManifest();
        Assert.True(trip.AttachManifest(linked.Id).IsSuccess);
        var byNumberOnly = EmptyManifest();
        trip.ClearDomainEvents();

        var result = Convert(trip, [linked, byNumberOnly]);

        Assert.True(result.IsSuccess);
        Assert.True(trip.IsEmptyLeg);
        Assert.Null(trip.ManifestId);
        var raised = Assert.Single(trip.DomainEvents.OfType<TripConvertedToDeadheadDomainEvent>());
        Assert.Equal(trip.Id, raised.TripId);
        Assert.Equal([linked.Id, byNumberOnly.Id], raised.RemovedManifestIds);
    }

    [Fact]
    public void Converting_leaves_seats_client_assignment_and_pairing_untouched()
    {
        var trip = ScheduledTrip(roundTripKey: "merge:abc");
        var partner = ScheduledTrip(tripNumber: "TR-1002", roundTripKey: "merge:abc");

        Assert.True(Convert(trip, partners: [partner]).IsSuccess);

        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Equal(ClientId, trip.ClientId);
        Assert.Equal(TestPlanning.DriverId, trip.DriverId);
        Assert.Equal(TestPlanning.VehicleId, trip.VehicleId);
        Assert.Equal(12, trip.SeatsCapacity);
        Assert.Equal("merge:abc", trip.RoundTripKey);
        Assert.False(partner.IsEmptyLeg);
    }

    [Theory]
    [InlineData(TripServiceType.Cargo)]
    [InlineData(TripServiceType.Grocery)]
    [InlineData(TripServiceType.Community)]
    public void Any_service_type_may_convert_including_cargo_and_grocery(TripServiceType serviceType)
    {
        var trip = ScheduledTrip(serviceType: serviceType);

        Assert.True(Convert(trip).IsSuccess);
        Assert.True(trip.IsEmptyLeg);
    }

    [Fact]
    public void A_trip_with_no_manifest_at_all_converts_and_the_event_lists_none()
    {
        var trip = ScheduledTrip();

        Assert.True(Convert(trip).IsSuccess);

        Assert.Empty(Assert.Single(trip.DomainEvents.OfType<TripConvertedToDeadheadDomainEvent>()).RemovedManifestIds);
    }

    // ------------------------------------------------------------------ refusals, one per rule

    [Fact]
    public void Refused_unless_scheduled()
    {
        var trip = ScheduledTrip();
        Assert.True(trip.Start().IsSuccess);

        Assert.Equal(TripErrors.DeadheadConversionNotScheduled, Convert(trip).Error);
        Assert.False(trip.IsEmptyLeg);
    }

    [Fact]
    public void Refused_when_already_a_deadhead()
    {
        var trip = ScheduledTrip(isEmptyLeg: true);

        Assert.Equal(TripErrors.AlreadyEmptyLeg, Convert(trip).Error);
        Assert.Empty(trip.DomainEvents);
    }

    [Fact]
    public void Refused_for_a_trip_confirmed_from_a_booking_day()
    {
        var trip = Trip.ScheduleFromBooking(
            TestPlanning.TenantId, "TR-1001", Guid.NewGuid(), new DateOnly(2026, 7, 21), new TimeOnly(8, 0),
            Guid.NewGuid(), "Thompson ↔ Lynn Lake", "Thompson", "Lynn Lake", TestPlanning.Stops(), 320,
            seatsConfirmed: 0, seatsCapacity: 12, seatsMinimum: 4).Value;

        Assert.Equal(TripErrors.DeadheadConversionBookingSourced.Code, Convert(trip).Error.Code);
        Assert.False(trip.IsEmptyLeg);
    }

    [Fact]
    public void Refused_with_confirmed_seats_and_the_seats_are_not_reset()
    {
        var trip = ScheduledTrip();
        Assert.True(trip.RecordDemand(3, demandGuaranteed: false).IsSuccess);

        var error = Convert(trip).Error;

        Assert.Equal("Trips.Trip.DeadheadConversionHasDemand", error.Code);
        Assert.Contains("3 confirmed seats", error.Message, StringComparison.Ordinal);
        Assert.Equal(3, trip.SeatsConfirmed);
        Assert.False(trip.IsEmptyLeg);
    }

    [Fact]
    public void Refused_with_a_gift_a_seat_pledge_even_at_zero_seats()
    {
        var trip = ScheduledTrip();
        Assert.True(trip.RecordDemand(0, demandGuaranteed: true).IsSuccess);

        var error = Convert(trip).Error;

        Assert.Equal("Trips.Trip.DeadheadConversionHasDemand", error.Code);
        Assert.Contains("gift-a-seat", error.Message, StringComparison.Ordinal);
        Assert.True(trip.DemandGuaranteed);
    }

    [Fact]
    public void Refused_when_any_manifest_lists_a_passenger_with_the_counts_in_the_message()
    {
        var trip = ScheduledTrip();
        var withPassenger = TestManifests.Create(tripNumber: "TR-1001").Value;

        var error = Convert(trip, [EmptyManifest(), withPassenger]).Error;

        Assert.Equal("Trips.Trip.DeadheadConversionManifestNotEmpty", error.Code);
        Assert.Contains("1 passenger and 0 cargo items", error.Message, StringComparison.Ordinal);
        Assert.False(trip.IsEmptyLeg);
    }

    [Fact]
    public void Refused_when_a_manifest_carries_cargo_items_only()
    {
        var trip = ScheduledTrip();
        var cargoOnly = TestManifests.Create(
            tripNumber: "TR-1001",
            passengers: [],
            cargo: [new ManifestCargoItem { Description = "Groceries" }, new ManifestCargoItem { Description = "Mail" }]).Value;

        var error = Convert(trip, [cargoOnly]).Error;

        Assert.Equal("Trips.Trip.DeadheadConversionManifestNotEmpty", error.Code);
        Assert.Contains("0 passengers and 2 cargo items", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refused_when_imported_bookeo_bookings_point_at_the_trip()
    {
        var trip = ScheduledTrip();

        var error = Convert(trip, bookings: 2).Error;

        Assert.Equal("Trips.Trip.DeadheadConversionHasExternalBookings", error.Code);
        Assert.Contains("2 imported Bookeo bookings", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refused_when_the_paired_leg_is_already_a_deadhead()
    {
        var source = ScheduledTrip();
        var deadhead = source.DeadheadReturn("TR-1002").Value;

        var error = Convert(source, partners: [deadhead]).Error;

        Assert.Equal("Trips.Trip.RoundTripBothLegsEmpty", error.Code);
        Assert.Contains("TR-1002", error.Message, StringComparison.Ordinal);
        Assert.False(source.IsEmptyLeg);
    }

    // ------------------------------------------------------------------ converting back

    [Fact]
    public void Converting_back_clears_only_the_flag_and_raises_the_event()
    {
        var trip = ScheduledTrip();
        Assert.True(Convert(trip).IsSuccess);
        trip.ClearDomainEvents();

        var result = trip.ConvertToPassengerTrip();

        Assert.True(result.IsSuccess);
        Assert.False(trip.IsEmptyLeg);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Null(trip.ManifestId);
        Assert.Equal(trip.Id, Assert.Single(trip.DomainEvents.OfType<TripConvertedToPassengerTripDomainEvent>()).TripId);
    }

    [Fact]
    public void Converting_back_is_refused_for_a_trip_that_is_not_a_deadhead()
    {
        var trip = ScheduledTrip();

        Assert.Equal(TripErrors.NotEmptyLeg, trip.ConvertToPassengerTrip().Error);
        Assert.Empty(trip.DomainEvents);
    }

    [Fact]
    public void Converting_back_is_refused_unless_scheduled()
    {
        var trip = ScheduledTrip(isEmptyLeg: true);
        Assert.True(trip.Start().IsSuccess);

        Assert.Equal(TripErrors.PassengerTripConversionNotScheduled, trip.ConvertToPassengerTrip().Error);
        Assert.True(trip.IsEmptyLeg);
    }

    /// <summary>A dispatch caller, so the status handler's identity gate stays out of these gate tests.</summary>
    private static TripOperatorAccess DispatchAccess() =>
        new(FakeCurrentActor.Dispatcher, new FakeDriverLookupRepository());

    [Fact]
    public async Task After_converting_back_the_start_gate_and_the_post_trip_inspection_gate_apply_again()
    {
        var trips = new FakeTripRepository();
        var manifests = new FakeTripManifestRepository();
        var statusHandler = new ChangeTripStatusCommandHandler(trips, manifests, new FakeShipmentRepository(), DispatchAccess());
        var trip = ScheduledTrip();
        trips.Add(trip);
        Assert.True(Convert(trip).IsSuccess);

        Assert.True(trip.ConvertToPassengerTrip().IsSuccess);

        var start = await statusHandler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.Equal(TripErrors.PassengerManifestRequired, start.Error);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Equal(TripErrors.PostTripInspectionRequired, trip.FinishOperations().Error);
    }

    [Fact]
    public async Task While_a_deadhead_the_start_gate_does_not_apply()
    {
        var trips = new FakeTripRepository();
        var statusHandler = new ChangeTripStatusCommandHandler(
            trips, new FakeTripManifestRepository(), new FakeShipmentRepository(), DispatchAccess());
        var trip = ScheduledTrip();
        trips.Add(trip);
        Assert.True(Convert(trip).IsSuccess);

        var start = await statusHandler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(start.IsSuccess);
        Assert.Equal(TripStatus.InProgress, trip.Status);
    }
}
