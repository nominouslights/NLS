using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using Xunit;

namespace NorthernLink.Shared.Tests;

public class IntegrationEventSubscriptionsTests
{
    private sealed class TestHandler : IIntegrationEventHandler<VehicleStatusChangedIntegrationEvent>
    {
        public Task Handle(VehicleStatusChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private static IntegrationEventSubscriptions Build(IServiceCollection services)
    {
        var subscriptions = new IntegrationEventSubscriptions("trips");
        new IntegrationEventSubscriptionsBuilder(services, subscriptions)
            .On<VehicleStatusChangedIntegrationEvent, TestHandler>();
        return subscriptions;
    }

    [Fact]
    public void Queue_names_follow_module_convention()
    {
        var subscriptions = Build(new ServiceCollection());

        Assert.Equal("northernlink.trips", subscriptions.QueueName);
        Assert.Equal("northernlink.trips.dead-letter", subscriptions.DeadLetterQueueName);
    }

    [Fact]
    public void Routing_key_derives_from_event_type()
    {
        var subscriptions = Build(new ServiceCollection());

        Assert.Contains("fleet.vehicle-status-changed", subscriptions.RoutingKeys);
        Assert.Equal(typeof(VehicleStatusChangedIntegrationEvent), subscriptions.EventTypeFor("fleet.vehicle-status-changed"));
    }

    [Fact]
    public void Unknown_routing_keys_resolve_to_null()
    {
        var subscriptions = Build(new ServiceCollection());

        Assert.Null(subscriptions.EventTypeFor("fleet.vehicle-registered"));
    }

    [Fact]
    public void On_registers_the_handler_in_the_container()
    {
        var services = new ServiceCollection();
        Build(services);

        using var provider = services.BuildServiceProvider();
        var handlers = provider.GetServices<IIntegrationEventHandler<VehicleStatusChangedIntegrationEvent>>();

        Assert.Single(handlers);
    }
}
