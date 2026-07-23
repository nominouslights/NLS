using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Trips.Application.Manifests;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Manifests.Events;
using NorthernLink.Trips.Domain.Trips.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class TripsIntegrationEventMapperTests
{
    private readonly TripsIntegrationEventMapper _mapper = new();

    [Fact]
    public void Manifest_completion_maps_to_public_integration_event()
    {
        var preTrip = TestManifests.AllOkPreTrip();
        preTrip[3] = preTrip[3] with
        {
            Status = PreTripItemStatus.Fail,
            Severity = DefectSeverity.Minor,
            Note = "Streaking, replace blade",
        };
        var manifest = TestManifests.Create(preTrip: preTrip).Value;
        var domainEvent = (TripManifestCompletedDomainEvent)manifest.DomainEvents.Single();

        var result = _mapper.Map(domainEvent, manifest);

        var integrationEvent = Assert.IsType<TripManifestCompletedIntegrationEvent>(result);
        Assert.Equal(manifest.Id, integrationEvent.ManifestId);
        Assert.Equal(manifest.TenantId, integrationEvent.TenantId);
        Assert.Equal("TR-4818", integrationEvent.TripNumber);
        Assert.Equal("U-04", integrationEvent.Unit);
        Assert.Equal("J. Spence", integrationEvent.DriverName);
        Assert.Equal("App", integrationEvent.Source);
        Assert.Equal(118_204, integrationEvent.OdometerStartKm);
        Assert.Equal(118_346, integrationEvent.OdometerEndKm);
        Assert.Equal(28, integrationEvent.PreTripItems.Count);
        Assert.Equal(6, integrationEvent.PostTripItems.Count);

        var failed = Assert.Single(integrationEvent.PreTripItems, item => item.Status == "Fail");
        Assert.Equal("Wipers & washer fluid", failed.Item);
        Assert.Equal("Minor", failed.Severity);
        Assert.Equal("Streaking, replace blade", failed.Note);
    }

    [Fact]
    public void Trip_completion_maps_to_public_integration_event_with_the_full_billing_payload()
    {
        var trip = TestPlanning.ScheduleTrip(
            tripNumber: "TR-4821",
            scheduleTemplateId: Guid.NewGuid(),
            roundTripKey: "abc123:20260721",
            direction: TripDirection.Outbound).Value;
        trip.ClearDomainEvents();
        trip.Complete();
        var domainEvent = (TripCompletedDomainEvent)trip.DomainEvents.Single();

        var result = _mapper.Map(domainEvent, trip);

        var integrationEvent = Assert.IsType<TripCompletedIntegrationEvent>(result);
        Assert.Equal(trip.Id, integrationEvent.TripId);
        Assert.Equal(TestPlanning.TenantId, integrationEvent.TenantId);
        Assert.Equal("TR-4821", integrationEvent.TripNumber);
        Assert.Null(integrationEvent.ClientId);
        Assert.Equal("Alamos Gold", integrationEvent.ClientName);
        Assert.Equal("ContractCrew", integrationEvent.ServiceType);
        Assert.Equal("Thompson ↔ Lynn Lake", integrationEvent.RouteName);
        Assert.Equal("Thompson", integrationEvent.Origin);
        Assert.Equal("Lynn Lake", integrationEvent.Destination);
        Assert.Equal(320, integrationEvent.DistanceKm);
        Assert.Equal(new DateOnly(2026, 7, 21), integrationEvent.ServiceDate);
        Assert.Equal("abc123:20260721", integrationEvent.RoundTripKey);
        Assert.Equal("PO-2026-118", integrationEvent.PoNumber);
        Assert.Equal(trip.CompletedAtUtc, integrationEvent.CompletedAtUtc);
    }

    [Fact]
    public void Trip_completion_via_manifest_attachment_also_maps()
    {
        var trip = TestPlanning.ScheduleTrip(tripNumber: "TR-4822").Value;
        trip.ClearDomainEvents();
        trip.AttachManifest(Guid.NewGuid());
        var domainEvent = (TripCompletedDomainEvent)trip.DomainEvents.Single();

        var integrationEvent = Assert.IsType<TripCompletedIntegrationEvent>(_mapper.Map(domainEvent, trip));
        Assert.Equal("TR-4822", integrationEvent.TripNumber);
    }
}
