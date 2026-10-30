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
