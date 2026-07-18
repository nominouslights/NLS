using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Trips.Application.Manifests;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Manifests.Events;
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
}
