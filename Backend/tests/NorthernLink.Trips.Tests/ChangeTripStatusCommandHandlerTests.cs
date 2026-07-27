using NorthernLink.Trips.Application.Trips.ChangeStatus;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class ChangeTripStatusCommandHandlerTests
{
    private readonly FakeTripRepository _trips = new();
    private readonly FakeTripManifestRepository _manifests = new();

    private ChangeTripStatusCommandHandler Handler => new(_trips, _manifests);

    [Fact]
    public async Task Start_is_rejected_when_the_trip_has_no_manifest()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.PassengerManifestRequired, result.Error);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Start_is_rejected_when_the_manifest_has_zero_passengers()
    {
        var manifest = TestManifests.Create(passengers: []).Value;
        _manifests.Add(manifest);
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.AttachManifest(manifest.Id);
        _trips.Add(trip);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.PassengerManifestRequired, result.Error);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
    }

    [Fact]
    public async Task Start_is_allowed_with_a_manifest_carrying_at_least_one_passenger()
    {
        var manifest = TestManifests.Create().Value; // one passenger
        _manifests.Add(manifest);
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.AttachManifest(manifest.Id);
        _trips.Add(trip);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.InProgress, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.InProgress, trip.Status);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Complete_does_not_require_a_passenger_manifest()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        trip.RecordPostTripInspection(); // the completion gate is the post-trip inspection, not a manifest
        _trips.Add(trip);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.Completed, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Completed, trip.Status);
    }

    [Fact]
    public async Task Complete_is_rejected_without_a_logged_post_trip_inspection()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.Completed, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.PostTripInspectionRequired, result.Error);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Cancel_does_not_require_a_passenger_manifest()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        _trips.Add(trip);

        var result = await Handler.Handle(
            new ChangeTripStatusCommand(trip.Id, TripStatus.Cancelled, "Weather"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.Cancelled, trip.Status);
    }
}
