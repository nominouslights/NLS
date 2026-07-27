using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Trips.GetActivity;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class GetTripActivityQueryHandlerTests
{
    private readonly FakeTripRepository _trips = new();
    private readonly FakeTripActivityReadService _activity = new();

    private GetTripActivityQueryHandler Handler => new(_trips, _activity);

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
