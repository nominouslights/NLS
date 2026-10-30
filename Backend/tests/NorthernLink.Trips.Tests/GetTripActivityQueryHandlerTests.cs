using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Trips;
using NorthernLink.Trips.Application.Trips.GetActivity;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class GetTripActivityQueryHandlerTests
{
    private readonly FakeTripRepository _trips = new();
    private readonly FakeTripActivityReadService _activity = new();

    private readonly FakeDriverLookupRepository _drivers = new();

    private GetTripActivityQueryHandler Handler =>
        new(_trips, _activity, new TripOperatorAccess(FakeCurrentActor.Dispatcher, _drivers));

    private static readonly DateTimeOffset T0 = new(2026, 7, 21, 6, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddMinutes(5);
    private static readonly DateTimeOffset T2 = T0.AddMinutes(10);
    private static readonly DateTimeOffset T3 = T0.AddMinutes(15);

    [Fact]
    public async Task Unknown_trip_returns_not_found()
    {
        var result = await Handler.Handle(
            new GetTripActivityQuery(Guid.NewGuid(), TestPlanning.TenantId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Timeline_is_ordered_with_manifest_provenance_and_trip_events()
    {
        var manifestId = Guid.NewGuid();
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.AttachManifest(manifestId);
        _trips.Add(trip);

        // Deliberately out of chronological order — the handler must sort by OccurredAtUtc.
        _activity.Entries.AddRange(
        [
            // Trip status change (a lifecycle event) — no provenance.
            new TripActivityJournalEntry(T3, "trip", "trip-status-changed",
                $$"""{"tripId":"{{trip.Id}}","occurredAtUtc":"{{T3:O}}"}"""),
            // Manifest edit from the Dispatch Console — provenance populated.
            new TripActivityJournalEntry(T2, "trip-manifest", "trip-manifest-updated",
                $$"""{"manifestId":"{{manifestId}}","source":"Dispatcher","enteredBy":"dispatch@northernlink.ca","occurredAtUtc":"{{T2:O}}"}"""),
            // Trip scheduled (a lifecycle event) — no provenance.
            new TripActivityJournalEntry(T0, "trip", "trip-scheduled",
                $$"""{"tripId":"{{trip.Id}}","occurredAtUtc":"{{T0:O}}"}"""),
            // Manifest created — the created event carries no source/enteredBy, so both stay null.
            new TripActivityJournalEntry(T1, "trip-manifest", "trip-manifest-created",
                $$"""{"manifestId":"{{manifestId}}","occurredAtUtc":"{{T1:O}}"}"""),
        ]);

        var result = await Handler.Handle(
            new GetTripActivityQuery(trip.Id, TestPlanning.TenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        // The handler passed the trip's attached manifest id down to the read side.
        Assert.Equal(trip.Id, _activity.RequestedTripId);
        Assert.Equal(manifestId, _activity.RequestedManifestId);

        var timeline = result.Value;
        Assert.Equal(4, timeline.Count);

        // Ordered oldest-first by OccurredAtUtc.
        Assert.Equal(new[] { T0, T1, T2, T3 }, timeline.Select(e => e.OccurredAtUtc));
        Assert.Equal(
            new[] { "trip-scheduled", "trip-manifest-created", "trip-manifest-updated", "trip-status-changed" },
            timeline.Select(e => e.EventType));

        // Trip lifecycle events never carry provenance.
        var scheduled = timeline[0];
        Assert.Null(scheduled.Source);
        Assert.Null(scheduled.EnteredBy);

        // Manifest-created carries no provenance either.
        var created = timeline[1];
        Assert.Equal("trip-manifest", created.AggregateType);
        Assert.Null(created.Source);
        Assert.Null(created.EnteredBy);

        // Manifest-updated carries source + who from its payload.
        var updated = timeline[2];
        Assert.Equal("trip-manifest", updated.AggregateType);
        Assert.Equal("Dispatcher", updated.Source);
        Assert.Equal("dispatch@northernlink.ca", updated.EnteredBy);

        var statusChanged = timeline[3];
        Assert.Null(statusChanged.Source);
        Assert.Null(statusChanged.EnteredBy);
    }

    [Fact]
    public async Task A_driver_correction_shows_reason_who_and_the_before_after_detail()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);
        _activity.Entries.Add(new TripActivityJournalEntry(T1, "trip", "trip-driver-corrected",
            $$"""
            {"tripId":"{{trip.Id}}","previousDriverId":"{{TestPlanning.DriverId}}","previousDriverName":"R. Ballantyne",
             "newDriverId":"{{Guid.NewGuid()}}","newDriverName":"J. Moose","reason":"Logged against the wrong driver",
             "enteredByUserId":"{{Guid.NewGuid()}}","enteredBy":"dispatch@northernlink.ca","occurredAtUtc":"{{T1:O}}"}
            """));

        var entry = Assert.Single((await Handler.Handle(
            new GetTripActivityQuery(trip.Id, TestPlanning.TenantId), CancellationToken.None)).Value);

        Assert.Equal("trip-driver-corrected", entry.EventType);
        Assert.Equal("Logged against the wrong driver", entry.Reason);
        Assert.Equal("dispatch@northernlink.ca", entry.EnteredBy);
        Assert.Equal("Driver: R. Ballantyne → J. Moose", entry.Detail);
        Assert.Null(entry.Source);
    }

    [Fact]
    public async Task A_correction_of_a_trip_with_no_previous_driver_reads_none()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);
        _activity.Entries.Add(new TripActivityJournalEntry(T1, "trip", "trip-driver-corrected",
            """{"previousDriverId":null,"previousDriverName":null,"newDriverName":"J. Moose","reason":"no driver logged"}"""));

        var entry = Assert.Single((await Handler.Handle(
            new GetTripActivityQuery(trip.Id, TestPlanning.TenantId), CancellationToken.None)).Value);

        Assert.Equal("Driver: none → J. Moose", entry.Detail);
    }

    [Fact]
    public async Task A_reason_on_any_trip_event_surfaces_generically_without_a_detail()
    {
        // e.g. the reopen work's trip-reopened: the timeline needs no per-event code for a reason.
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);
        _activity.Entries.Add(new TripActivityJournalEntry(T1, "trip", "trip-some-future-event",
            """{"reason":"cancelled by mistake","previousStatus":"Cancelled"}"""));

        var entry = Assert.Single((await Handler.Handle(
            new GetTripActivityQuery(trip.Id, TestPlanning.TenantId), CancellationToken.None)).Value);

        Assert.Equal("cancelled by mistake", entry.Reason);
        Assert.Null(entry.Detail);
        Assert.Null(entry.EnteredBy);
    }

    [Fact]
    public async Task Other_entries_are_unchanged_and_malformed_payloads_do_not_throw()
    {
        var manifestId = Guid.NewGuid();
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.AttachManifest(manifestId);
        _trips.Add(trip);
        _activity.Entries.AddRange(
        [
            new TripActivityJournalEntry(T0, "trip", "trip-scheduled", $$"""{"tripId":"{{trip.Id}}"}"""),
            new TripActivityJournalEntry(T1, "trip", "trip-driver-corrected", "not json"),
            // A manifest event's "reason" is not a trip reason — manifests keep their own provenance only.
            new TripActivityJournalEntry(T2, "trip-manifest", "trip-manifest-updated",
                """{"source":"App","enteredBy":"driver@northernlink.ca","reason":"ignored"}"""),
        ]);

        var timeline = (await Handler.Handle(
            new GetTripActivityQuery(trip.Id, TestPlanning.TenantId), CancellationToken.None)).Value;

        Assert.All(timeline.Take(2), e => Assert.True(e is { Reason: null, Detail: null, EnteredBy: null, Source: null }));
        Assert.Equal("App", timeline[2].Source);
        Assert.Equal("driver@northernlink.ca", timeline[2].EnteredBy);
        Assert.Null(timeline[2].Reason);
        Assert.Null(timeline[2].Detail);
    }

    [Fact]
    public async Task The_trips_own_driver_sees_trip_events_without_reason_detail_or_who()
    {
        var driverUserId = Guid.Parse("00000000-0000-0000-0000-00000000ab07");
        _drivers.Drivers.Add(TestPlanning.ActiveDriver(userId: driverUserId));
        var manifestId = Guid.NewGuid();
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.AttachManifest(manifestId);
        _trips.Add(trip);
        _activity.Entries.AddRange(
        [
            new TripActivityJournalEntry(T1, "trip", "trip-driver-corrected",
                """{"previousDriverName":"R. Ballantyne","newDriverName":"J. Moose","reason":"Logged against the wrong driver","enteredBy":"dispatch@northernlink.ca"}"""),
            new TripActivityJournalEntry(T2, "trip", "trip-some-future-event",
                """{"reason":"cancelled by mistake","enteredBy":"dispatch@northernlink.ca"}"""),
            // Manifest rows keep their existing behaviour for drivers.
            new TripActivityJournalEntry(T3, "trip-manifest", "trip-manifest-updated",
                """{"source":"Dispatcher","enteredBy":"dispatch@northernlink.ca"}"""),
        ]);

        var asDriver = new GetTripActivityQueryHandler(
            _trips, _activity, new TripOperatorAccess(FakeCurrentActor.Driver(driverUserId), _drivers));
        var timeline = (await asDriver.Handle(
            new GetTripActivityQuery(trip.Id, TestPlanning.TenantId), CancellationToken.None)).Value;

        Assert.Equal(3, timeline.Count);
        Assert.Equal(new[] { "trip-driver-corrected", "trip-some-future-event" }, timeline.Take(2).Select(e => e.EventType));
        Assert.All(timeline.Take(2), e => Assert.True(
            e is { AggregateType: "trip", Reason: null, Detail: null, EnteredBy: null, Source: null }));
        Assert.Equal("Dispatcher", timeline[2].Source);
        Assert.Equal("dispatch@northernlink.ca", timeline[2].EnteredBy);

        // The same journal read by dispatch is unredacted.
        var asDispatch = (await Handler.Handle(
            new GetTripActivityQuery(trip.Id, TestPlanning.TenantId), CancellationToken.None)).Value;
        Assert.Equal("Logged against the wrong driver", asDispatch[0].Reason);
        Assert.Equal("Driver: R. Ballantyne → J. Moose", asDispatch[0].Detail);
        Assert.Equal("dispatch@northernlink.ca", asDispatch[0].EnteredBy);
    }

    [Fact]
    public void The_journal_names_the_handler_derives_are_the_stored_literals()
    {
        // The handler derives these from AuditNames; pinning the literals here means renaming
        // Trip, TripManifest or TripDriverCorrectedDomainEvent fails a test instead of silently
        // orphaning every journal row already written under the old name.
        Assert.Equal("trip", Shared.Persistence.Auditing.AuditNames.ForAggregate(typeof(Trip)));
        Assert.Equal("trip-manifest",
            Shared.Persistence.Auditing.AuditNames.ForAggregate(typeof(Domain.Manifests.TripManifest)));
        Assert.Equal("trip-driver-corrected",
            Shared.Persistence.Auditing.AuditNames.ForEvent(typeof(Domain.Trips.Events.TripDriverCorrectedDomainEvent)));
    }

    [Fact]
    public async Task Trip_without_a_manifest_passes_null_manifest_id()
    {
        var trip = TestPlanning.ScheduleTrip().Value; // no manifest attached
        _trips.Add(trip);

        var result = await Handler.Handle(
            new GetTripActivityQuery(trip.Id, TestPlanning.TenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(trip.Id, _activity.RequestedTripId);
        Assert.Null(_activity.RequestedManifestId);
    }
}
