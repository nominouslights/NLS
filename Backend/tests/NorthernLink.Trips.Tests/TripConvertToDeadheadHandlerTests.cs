using NorthernLink.Trips.Application.Trips.ConvertToDeadhead;
using NorthernLink.Trips.Application.Trips.ConvertToPassengerTrip;
using NorthernLink.Trips.Domain.BookeoImports;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The application half: the handler must find EVERY manifest for the trip (by id and by trip
/// number — linking is lazy), the live Bookeo ledger rows, and the paired leg, delete the empty
/// manifests in the same save, and map a lost version race to a clean 409.
/// </summary>
public class TripConvertToDeadheadHandlerTests
{
    private static readonly Guid ClientId = Guid.Parse("00000000-0000-0000-0000-0000000000c1");

    private readonly FakeTripRepository _trips = new();
    private readonly FakeTripManifestRepository _manifests = new();
    private readonly FakeBookeoImportRepository _bookeo = new();

    private ConvertTripToDeadheadCommandHandler Handler => new(_trips, _manifests, _bookeo);

    private ConvertTripToPassengerTripCommandHandler BackHandler => new(_trips);

    private Trip AddTrip(string tripNumber = "TR-1001", string? roundTripKey = null)
    {
        var trip = TestPlanning.ScheduleTrip(tripNumber: tripNumber, roundTripKey: roundTripKey, clientId: ClientId).Value;
        trip.ClearDomainEvents();
        _trips.Add(trip);
        return trip;
    }

    private TripManifest AddManifest(string tripNumber, bool withPassenger = false)
    {
        var manifest = TestManifests.Create(tripNumber: tripNumber, passengers: withPassenger ? null : []).Value;
        _manifests.Add(manifest);
        return manifest;
    }

    private static BookeoBooking Booking(string number, Guid? tripId) => BookeoBooking.Record(
        TestPlanning.TenantId,
        new BookeoBookingSnapshot(
            number, "P1", "Shuttle from Thompson", null, new DateOnly(2026, 7, 21), new TimeOnly(6, 30), null,
            "Confirmed", 1, [new BookeoBookingPassenger { Name = "A. Rider" }], "A. Rider", null, null,
            120m, 120m, 0m, null, "hash"),
        tripId,
        Guid.NewGuid(),
        DateTimeOffset.UtcNow);

    private Task<NorthernLink.Shared.Kernel.Result> Convert(Trip trip) =>
        Handler.Handle(new ConvertTripToDeadheadCommand(trip.Id), CancellationToken.None);

    [Fact]
    public async Task Empty_manifests_found_by_id_and_by_trip_number_are_deleted_in_the_same_save()
    {
        var trip = AddTrip();
        var linked = AddManifest("TR-1001");
        Assert.True(trip.AttachManifest(linked.Id).IsSuccess);
        var byNumberOnly = AddManifest("TR-1001");
        var otherTrip = AddManifest("TR-9999");

        var result = await Convert(trip);

        Assert.True(result.IsSuccess);
        Assert.True(trip.IsEmptyLeg);
        Assert.Null(trip.ManifestId);
        Assert.Equal([linked.Id, byNumberOnly.Id], _manifests.Removed.Select(m => m.Id));
        Assert.Equal([otherTrip], _manifests.Manifests);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task A_manifest_linked_by_id_under_a_different_trip_number_is_still_found()
    {
        var trip = AddTrip();
        var renumbered = AddManifest("TR-OLD");
        Assert.True(trip.AttachManifest(renumbered.Id).IsSuccess);

        Assert.True((await Convert(trip)).IsSuccess);

        Assert.Equal([renumbered.Id], _manifests.Removed.Select(m => m.Id));
    }

    [Fact]
    public async Task A_non_empty_manifest_linked_only_by_trip_number_refuses_and_nothing_is_deleted()
    {
        var trip = AddTrip();
        var linked = AddManifest("TR-1001");
        Assert.True(trip.AttachManifest(linked.Id).IsSuccess);
        AddManifest("TR-1001", withPassenger: true);

        var result = await Convert(trip);

        Assert.Equal("Trips.Trip.DeadheadConversionManifestNotEmpty", result.Error.Code);
        Assert.False(trip.IsEmptyLeg);
        Assert.Equal(linked.Id, trip.ManifestId);
        Assert.Empty(_manifests.Removed);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Live_bookeo_ledger_rows_on_the_trip_refuse()
    {
        var trip = AddTrip();
        _bookeo.Bookings.Add(Booking("9000000000000001", trip.Id));

        var result = await Convert(trip);

        Assert.Equal("Trips.Trip.DeadheadConversionHasExternalBookings", result.Error.Code);
        Assert.Contains("1 imported Bookeo booking is", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Cancelled_bookeo_bookings_have_no_trip_and_do_not_block()
    {
        var trip = AddTrip();
        _bookeo.Bookings.Add(Booking("9000000000000002", tripId: null));
        _bookeo.Bookings.Add(Booking("9000000000000003", Guid.NewGuid()));

        Assert.True((await Convert(trip)).IsSuccess);
    }

    [Fact]
    public async Task A_partner_leg_that_is_already_a_deadhead_refuses()
    {
        var source = AddTrip();
        var deadhead = source.DeadheadReturn("TR-1002").Value;
        _trips.Add(deadhead);

        var result = await Convert(source);

        Assert.Equal("Trips.Trip.RoundTripBothLegsEmpty", result.Error.Code);
        Assert.False(source.IsEmptyLeg);
    }

    [Fact]
    public async Task A_paired_trip_whose_partner_carries_passengers_converts()
    {
        var outbound = AddTrip("TR-1001", roundTripKey: "merge:pair");
        var inbound = AddTrip("TR-1002", roundTripKey: "merge:pair");

        Assert.True((await Convert(outbound)).IsSuccess);

        Assert.True(outbound.IsEmptyLeg);
        Assert.False(inbound.IsEmptyLeg);
    }

    [Fact]
    public async Task A_lost_version_race_is_ChangedConcurrently()
    {
        var trip = AddTrip();
        AddManifest("TR-1001");
        _trips.SimulateConcurrencyConflict = true;

        var result = await Convert(trip);

        Assert.Equal(TripErrors.ChangedConcurrently, result.Error);
    }

    [Fact]
    public async Task An_unknown_trip_is_NotFound()
    {
        var result = await Handler.Handle(new ConvertTripToDeadheadCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(TripErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Converting_back_saves_and_unknown_or_non_deadhead_trips_are_refused()
    {
        var trip = AddTrip();
        Assert.True((await Convert(trip)).IsSuccess);

        var back = await BackHandler.Handle(new ConvertTripToPassengerTripCommand(trip.Id), CancellationToken.None);
        var again = await BackHandler.Handle(new ConvertTripToPassengerTripCommand(trip.Id), CancellationToken.None);
        var unknown = await BackHandler.Handle(new ConvertTripToPassengerTripCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(back.IsSuccess);
        Assert.False(trip.IsEmptyLeg);
        Assert.Equal(TripErrors.NotEmptyLeg, again.Error);
        Assert.Equal(TripErrors.NotFound, unknown.Error);
        Assert.Equal(2, _trips.SaveCount);
    }

    [Fact]
    public async Task Converting_back_maps_a_lost_version_race_to_ChangedConcurrently()
    {
        var trip = AddTrip();
        Assert.True((await Convert(trip)).IsSuccess);
        _trips.SimulateConcurrencyConflict = true;

        var back = await BackHandler.Handle(new ConvertTripToPassengerTripCommand(trip.Id), CancellationToken.None);

        Assert.Equal(TripErrors.ChangedConcurrently, back.Error);
    }
}
