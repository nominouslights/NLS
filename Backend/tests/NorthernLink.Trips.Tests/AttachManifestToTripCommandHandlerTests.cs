using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Trips.Application.Trips.AttachManifest;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class AttachManifestToTripCommandHandlerTests
{
    private readonly FakeTripManifestRepository _manifests = new();
    private readonly FakeTripRepository _trips = new();

    private AttachManifestToTripCommandHandler Handler =>
        new(_manifests, _trips, NullLogger<AttachManifestToTripCommandHandler>.Instance);

    [Fact]
    public async Task Missing_manifest_is_a_success_no_op()
    {
        var result = await Handler.Handle(
            new AttachManifestToTripCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Manifest_without_a_matching_trip_is_a_success_no_op()
    {
        var manifest = TestManifests.Create(tripNumber: "TR-9999").Value;
        _manifests.Add(manifest);

        var result = await Handler.Handle(
            new AttachManifestToTripCommand(manifest.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Matching_trip_gets_the_manifest_linked_without_completing()
    {
        var manifest = TestManifests.Create(tripNumber: "TR-4821").Value;
        _manifests.Add(manifest);
        var trip = TestPlanning.ScheduleTrip(tripNumber: "TR-4821").Value;
        _trips.Add(trip);

        var result = await Handler.Handle(
            new AttachManifestToTripCommand(manifest.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(manifest.Id, trip.ManifestId);
        Assert.Equal(TripStatus.Scheduled, trip.Status); // linking never completes
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Redelivery_of_the_same_manifest_converges()
    {
        var manifest = TestManifests.Create(tripNumber: "TR-4821").Value;
        _manifests.Add(manifest);
        var trip = TestPlanning.ScheduleTrip(tripNumber: "TR-4821").Value;
        _trips.Add(trip);

        var command = new AttachManifestToTripCommand(manifest.Id);
        Assert.True((await Handler.Handle(command, CancellationToken.None)).IsSuccess);
        trip.ClearDomainEvents();

        var second = await Handler.Handle(command, CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Empty(trip.DomainEvents); // no second link event
    }

    [Fact]
    public async Task A_second_different_manifest_for_the_same_trip_number_is_a_conflict()
    {
        var first = TestManifests.Create(tripNumber: "TR-4821").Value;
        var second = TestManifests.Create(tripNumber: "TR-4821").Value;
        _manifests.Add(first);
        _manifests.Add(second);
        var trip = TestPlanning.ScheduleTrip(tripNumber: "TR-4821").Value;
        _trips.Add(trip);

        Assert.True((await Handler.Handle(new AttachManifestToTripCommand(first.Id), CancellationToken.None)).IsSuccess);

        var result = await Handler.Handle(new AttachManifestToTripCommand(second.Id), CancellationToken.None);

        Assert.Equal(TripErrors.ManifestAlreadyAttached, result.Error);
        Assert.Equal(first.Id, trip.ManifestId);
    }
}
