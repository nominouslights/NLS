using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Compliance;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using Xunit;

namespace NorthernLink.Shared.Tests;

/// <summary>
/// The outbox-polling consumer reads each routing key from the schema its first segment names —
/// unless the subscription says otherwise. <c>OnProducedBy</c> exists for a contract event that
/// lives in the consumer's namespace but is written by other modules' outboxes.
/// </summary>
public class IntegrationEventSubscriptionsProducerSchemaTests
{
    private sealed class FleetHandler : IIntegrationEventHandler<VehicleStatusChangedIntegrationEvent>
    {
        public Task Handle(VehicleStatusChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ProducedHandler : IIntegrationEventHandler<ComplianceRecordProducedIntegrationEvent>
    {
        public Task Handle(ComplianceRecordProducedIntegrationEvent integrationEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void By_default_a_key_is_polled_from_its_own_first_segment()
    {
        var subscriptions = new IntegrationEventSubscriptions("trips");
        new IntegrationEventSubscriptionsBuilder(new ServiceCollection(), subscriptions)
            .On<VehicleStatusChangedIntegrationEvent, FleetHandler>();

        var bySchema = subscriptions.RoutingKeysBySchema;

        Assert.Equal(["fleet"], bySchema.Keys);
        Assert.Equal(["fleet.vehicle-status-changed"], bySchema["fleet"]);
    }

    [Fact]
    public void OnProducedBy_polls_the_named_producer_schemas_instead()
    {
        var services = new ServiceCollection();
        var subscriptions = new IntegrationEventSubscriptions("compliance");
        new IntegrationEventSubscriptionsBuilder(services, subscriptions)
            .OnProducedBy<ComplianceRecordProducedIntegrationEvent, ProducedHandler>("drivers", "fleet", "trips");

        var bySchema = subscriptions.RoutingKeysBySchema;

        Assert.Equal(["drivers", "fleet", "trips"], bySchema.Keys.Order());
        Assert.All(bySchema.Values, keys => Assert.Equal(["compliance.compliance-record-produced"], keys));
        Assert.DoesNotContain("compliance", bySchema.Keys);
        Assert.Equal(typeof(ComplianceRecordProducedIntegrationEvent), subscriptions.EventTypeFor("compliance.compliance-record-produced"));

        using var provider = services.BuildServiceProvider();
        Assert.Single(provider.GetServices<IIntegrationEventHandler<ComplianceRecordProducedIntegrationEvent>>());
    }

    [Fact]
    public void A_default_and_an_explicit_subscription_on_the_same_schema_merge()
    {
        var subscriptions = new IntegrationEventSubscriptions("compliance");
        new IntegrationEventSubscriptionsBuilder(new ServiceCollection(), subscriptions)
            .On<VehicleStatusChangedIntegrationEvent, FleetHandler>()
            .OnProducedBy<ComplianceRecordProducedIntegrationEvent, ProducedHandler>("fleet");

        var fleetKeys = subscriptions.RoutingKeysBySchema["fleet"];

        Assert.Equal(["compliance.compliance-record-produced", "fleet.vehicle-status-changed"], fleetKeys.Order());
    }

    [Fact]
    public void OnProducedBy_requires_at_least_one_schema()
    {
        var subscriptions = new IntegrationEventSubscriptions("compliance");
        var builder = new IntegrationEventSubscriptionsBuilder(new ServiceCollection(), subscriptions);

        Assert.Throws<ArgumentException>(() => builder.OnProducedBy<ComplianceRecordProducedIntegrationEvent, ProducedHandler>());
    }
}

/// <summary>
/// The fan-out mode: a module joining a routing key another module already consumes must not
/// share the per-row <c>processing_status</c> (each would see only the rows it won). Fan-out keys
/// are therefore kept out of the status-tracked map entirely and listed on their own.
/// </summary>
public class IntegrationEventSubscriptionsFanOutTests
{
    private sealed class VehicleHandler : IIntegrationEventHandler<VehicleChangedIntegrationEvent>
    {
        public Task Handle(VehicleChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ProducedHandler : IIntegrationEventHandler<ComplianceRecordProducedIntegrationEvent>
    {
        public Task Handle(ComplianceRecordProducedIntegrationEvent integrationEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void A_fan_out_key_is_read_by_cursor_and_never_by_status()
    {
        var services = new ServiceCollection();
        var subscriptions = new IntegrationEventSubscriptions("compliance");
        new IntegrationEventSubscriptionsBuilder(services, subscriptions)
            .OnProducedBy<ComplianceRecordProducedIntegrationEvent, ProducedHandler>("fleet")
            .OnFanOut<VehicleChangedIntegrationEvent, VehicleHandler>();

        Assert.Equal(["compliance.compliance-record-produced"], subscriptions.RoutingKeysBySchema["fleet"]);
        Assert.Equal(["fleet.vehicle-changed"], subscriptions.FanOutRoutingKeysBySchema["fleet"]);
        Assert.Equal(typeof(VehicleChangedIntegrationEvent), subscriptions.EventTypeFor("fleet.vehicle-changed"));
        Assert.Contains("fleet.vehicle-changed", subscriptions.RoutingKeys);

        using var provider = services.BuildServiceProvider();
        Assert.Single(provider.GetServices<IIntegrationEventHandler<VehicleChangedIntegrationEvent>>());
    }

    [Fact]
    public void A_module_with_only_fan_out_subscriptions_has_no_status_tracked_schema()
    {
        var subscriptions = new IntegrationEventSubscriptions("compliance");
        new IntegrationEventSubscriptionsBuilder(new ServiceCollection(), subscriptions)
            .OnFanOut<VehicleChangedIntegrationEvent, VehicleHandler>();

        Assert.Empty(subscriptions.RoutingKeysBySchema);
        Assert.Equal(["fleet"], subscriptions.FanOutRoutingKeysBySchema.Keys);
    }
}
