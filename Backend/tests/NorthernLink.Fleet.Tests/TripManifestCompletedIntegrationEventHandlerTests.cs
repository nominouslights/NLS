using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Fleet.Application.Inspections;
using NorthernLink.Fleet.Domain.Inspections;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class TripManifestCompletedIntegrationEventHandlerTests
{
    private static readonly Guid ManifestId = Guid.Parse("00000000-0000-0000-0000-00000000aaaa");

    private static TripManifestCompletedIntegrationEvent Event(
        string source = "App",
        string? enteredBy = null,
        IReadOnlyList<TripManifestPreTripItem>? preTrip = null,
        IReadOnlyList<TripManifestPostTripItem>? postTrip = null) => new(
        ManifestId,
        TestVehicles.TenantId,
        "TR-4818",
        "U-04",
        "J. Spence",
        source,
        enteredBy,
        new DateOnly(2026, 7, 14),
        DateTimeOffset.UtcNow,
        OdometerStartKm: 118_204,
        OdometerEndKm: 118_346,
        preTrip ??
        [
            new TripManifestPreTripItem("Exterior & Mechanical", "Tires", "Ok", null, null),
            new TripManifestPreTripItem(
                "Exterior & Mechanical", "Wipers & washer fluid", "Fail", "Minor", "Streaking, replace blade"),
        ],
        postTrip ??
        [
            new TripManifestPostTripItem("Vehicle exterior — no new damage", true),
            new TripManifestPostTripItem("Keys returned / secured", true),
        ]);

    private static TripManifestCompletedIntegrationEventHandler Handler(InMemoryVehicleInspectionRepository repository) =>
        new(repository, NullLogger<TripManifestCompletedIntegrationEventHandler>.Instance);

    [Fact]
    public async Task A_completed_manifest_creates_one_pre_trip_and_one_post_trip_inspection()
    {
        var repository = new InMemoryVehicleInspectionRepository();

        await Handler(repository).Handle(Event(), CancellationToken.None);

        Assert.Equal(2, repository.Inspections.Count);
        Assert.Equal(1, repository.SaveChangesCallCount);

        var preTrip = Assert.Single(repository.Inspections, i => i.Type == InspectionType.PreTrip);
        Assert.Equal(TestVehicles.TenantId, preTrip.TenantId);
        Assert.Equal("U-04", preTrip.Unit);
        Assert.Equal("TR-4818", preTrip.TripNumber);
        Assert.Equal(ManifestId, preTrip.ManifestId);
        Assert.Equal(InspectionSource.DriverApp, preTrip.Source);
        Assert.Equal(118_204, preTrip.OdometerKm);
        Assert.Equal(InspectionResult.PassWithDefects, preTrip.Result);
        var defect = Assert.Single(preTrip.Defects);
        Assert.Equal("Wipers & washer fluid", defect.Item);
        Assert.Equal(InspectionDefectSeverity.Minor, defect.Severity);

        var postTrip = Assert.Single(repository.Inspections, i => i.Type == InspectionType.PostTrip);
        Assert.Equal(118_346, postTrip.OdometerKm);
        Assert.Equal(InspectionResult.Pass, postTrip.Result);
        Assert.Empty(postTrip.Defects);
    }

    [Fact]
    public async Task Redelivery_of_the_same_event_is_idempotent()
    {
        var repository = new InMemoryVehicleInspectionRepository();
        var handler = Handler(repository);

        await handler.Handle(Event(), CancellationToken.None);
        await handler.Handle(Event(), CancellationToken.None);

        Assert.Equal(2, repository.Inspections.Count);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Paper_source_maps_to_paper_transcription_with_entered_by()
    {
        var repository = new InMemoryVehicleInspectionRepository();

        await Handler(repository).Handle(
            Event(source: "Paper", enteredBy: "Dispatch — R. Ballantyne"), CancellationToken.None);

        Assert.All(repository.Inspections, inspection =>
        {
            Assert.Equal(InspectionSource.PaperTranscription, inspection.Source);
            Assert.Equal("Dispatch — R. Ballantyne", inspection.EnteredBy);
        });
    }

    [Fact]
    public async Task An_unconfirmed_post_trip_item_becomes_a_minor_defect()
    {
        var repository = new InMemoryVehicleInspectionRepository();

        await Handler(repository).Handle(
            Event(postTrip:
            [
                new TripManifestPostTripItem("Vehicle exterior — no new damage", true),
                new TripManifestPostTripItem("Vehicle secured / plugged in", false),
            ]),
            CancellationToken.None);

        var postTrip = Assert.Single(repository.Inspections, i => i.Type == InspectionType.PostTrip);
        Assert.Equal(InspectionResult.PassWithDefects, postTrip.Result);
        var defect = Assert.Single(postTrip.Defects);
        Assert.Equal("Vehicle secured / plugged in", defect.Item);
        Assert.Equal(InspectionDefectSeverity.Minor, defect.Severity);
        Assert.Equal(TripManifestCompletedIntegrationEventHandler.UnconfirmedPostTripNote, defect.Note);
    }

    [Fact]
    public async Task An_out_of_service_pre_trip_fail_derives_a_failed_inspection()
    {
        var repository = new InMemoryVehicleInspectionRepository();

        await Handler(repository).Handle(
            Event(preTrip:
            [
                new TripManifestPreTripItem(
                    "Exterior & Mechanical", "Brakes", "Fail", "OutOfService", "No pedal pressure"),
            ]),
            CancellationToken.None);

        var preTrip = Assert.Single(repository.Inspections, i => i.Type == InspectionType.PreTrip);
        Assert.Equal(InspectionResult.Fail, preTrip.Result);
    }
}
