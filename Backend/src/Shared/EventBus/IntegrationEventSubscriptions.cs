using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.EventBus;

/// <summary>
/// One module's integration-event subscriptions: which routing keys its RabbitMQ queue
/// binds and which event type each deserializes to. Routing keys derive from the event
/// type via <see cref="EventRoutingKey"/>, so publisher and subscriber can never drift.
/// </summary>
public sealed class IntegrationEventSubscriptions
{
    private readonly Dictionary<string, Type> _eventTypesByRoutingKey = [];

    internal IntegrationEventSubscriptions(string moduleName)
    {
        ModuleName = moduleName;
    }

    public string ModuleName { get; }

    /// <summary>Durable queue the module consumes from, e.g. "northernlink.trips".</summary>
    public string QueueName => $"northernlink.{ModuleName}";

    /// <summary>Where messages land after handler retries are exhausted, for operator inspection.</summary>
    public string DeadLetterQueueName => $"{QueueName}.dead-letter";

    public IReadOnlyCollection<string> RoutingKeys => _eventTypesByRoutingKey.Keys;

    internal void Add(Type eventType) => _eventTypesByRoutingKey[EventRoutingKey.For(eventType)] = eventType;

    public Type? EventTypeFor(string routingKey) =>
        _eventTypesByRoutingKey.TryGetValue(routingKey, out var eventType) ? eventType : null;
}

/// <summary>Fluent registration surface for <see cref="AddIntegrationEventConsumer"/>.</summary>
public sealed class IntegrationEventSubscriptionsBuilder
{
    private readonly IServiceCollection _services;
    private readonly IntegrationEventSubscriptions _subscriptions;

    internal IntegrationEventSubscriptionsBuilder(
        IServiceCollection services,
        IntegrationEventSubscriptions subscriptions)
    {
        _services = services;
        _subscriptions = subscriptions;
    }

    /// <summary>
    /// Subscribes the module to <typeparamref name="TEvent"/>, handled by
    /// <typeparamref name="THandler"/> (registered scoped; multiple handlers per event
    /// are allowed and all run). Handlers must be idempotent keyed on the event id —
    /// delivery is at-least-once.
    /// </summary>
    public IntegrationEventSubscriptionsBuilder On<TEvent, THandler>()
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        _services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        _subscriptions.Add(typeof(TEvent));
        return this;
    }
}

public static class IntegrationEventConsumerServiceCollectionExtensions
{
    /// <summary>
    /// Declares a module as an integration-event consumer: one durable queue
    /// (<c>northernlink.&lt;module&gt;</c>) bound to the platform exchange with the routing
    /// keys of the subscribed events, drained by one hosted
    /// <see cref="RabbitMqIntegrationEventConsumer"/>. Called from the module's DI
    /// extension; modules that consume nothing call nothing.
    /// </summary>
    public static IServiceCollection AddIntegrationEventConsumer(
        this IServiceCollection services,
        string moduleName,
        Action<IntegrationEventSubscriptionsBuilder> configure)
    {
        var subscriptions = new IntegrationEventSubscriptions(moduleName);
        configure(new IntegrationEventSubscriptionsBuilder(services, subscriptions));

        services.AddSingleton<IHostedService>(sp => new RabbitMqIntegrationEventConsumer(
            subscriptions,
            sp.GetRequiredService<RabbitMqOptions>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<RabbitMqIntegrationEventConsumer>>()));

        return services;
    }
}
