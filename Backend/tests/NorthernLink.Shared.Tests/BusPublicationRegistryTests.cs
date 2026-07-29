using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Shared.IntegrationEvents.Trips;
using Xunit;

namespace NorthernLink.Shared.Tests;

public class BusPublicationRegistryTests
{
    [Fact]
    public void Empty_registry_designates_nothing_for_the_bus()
    {
        var registry = new BusPublicationRegistry();

        Assert.Empty(registry.RoutingKeys);
    }

    [Fact]
    public void Keys_derive_from_event_types_via_the_routing_key_convention()
    {
        // Designation by TYPE, not by string, so the registry can never drift from the
        // wire convention the outbox rows were written with.
        var registry = new BusPublicationRegistry(
            typeof(VehicleChangedIntegrationEvent),
            typeof(TripCompletedIntegrationEvent));

        Assert.Equal(2, registry.RoutingKeys.Count);
        Assert.Contains("fleet.vehicle-changed", registry.RoutingKeys);
        Assert.Contains("trips.trip-completed", registry.RoutingKeys);
    }

    [Fact]
    public void Non_integration_event_types_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new BusPublicationRegistry(typeof(string)));
    }
}
