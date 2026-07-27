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
    public void Manifest_events_are_internal_and_do_not_publish()
    {
        // Since inspections detached from the manifest (Phase B), the manifest carries
        // nothing another module consumes — its events stay internal (map to null).
        var manifest = TestManifests.Create().Value;
        var created = (TripManifestCreatedDomainEvent)manifest.DomainEvents.Single();

        Assert.Null(_mapper.Map(created, manifest));
    }

    [Fact]
    public void Trip_completion_maps_to_public_integration_event_with_the_full_billing_payload()
    {
        var trip = TestPlanning.ScheduleTrip(
            tripNumber: "TR-4821",
            scheduleTemplateId: Guid.NewGuid(),
            roundTripKey: "abc123:20260721",
            direction: TripDirection.Outbound).Value;
        trip.RecordPostTripInspection();
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
        Assert.Equal("Outbound", integrationEvent.Direction);
        Assert.Equal("PO-2026-118", integrationEvent.PoNumber);
        Assert.Equal(trip.CompletedAtUtc, integrationEvent.CompletedAtUtc);
    }

    [Fact]
    public void Trip_completion_with_no_direction_maps_direction_to_null()
    {
        var trip = TestPlanning.ScheduleTrip(tripNumber: "TR-9001", direction: null).Value;
        trip.RecordPostTripInspection();
        trip.ClearDomainEvents();
        trip.Complete();
        var domainEvent = (TripCompletedDomainEvent)trip.DomainEvents.Single();

        var integrationEvent = Assert.IsType<TripCompletedIntegrationEvent>(_mapper.Map(domainEvent, trip));
        Assert.Null(integrationEvent.Direction);
    }
}
