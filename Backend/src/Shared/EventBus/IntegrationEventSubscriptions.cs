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
    private readonly Dictionary<string, string[]> _producerSchemasByRoutingKey = [];
    private readonly HashSet<string> _fanOutRoutingKeys = [];

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

    internal void AddProducedBy(Type eventType, string[] producerSchemas)
    {
        var routingKey = EventRoutingKey.For(eventType);
        _eventTypesByRoutingKey[routingKey] = eventType;
        _producerSchemasByRoutingKey[routingKey] = producerSchemas;
    }

    internal void AddFanOut(Type eventType)
    {
        var routingKey = EventRoutingKey.For(eventType);
        _eventTypesByRoutingKey[routingKey] = eventType;
        _fanOutRoutingKeys.Add(routingKey);
    }

    public Type? EventTypeFor(string routingKey) =>
        _eventTypesByRoutingKey.TryGetValue(routingKey, out var eventType) ? eventType : null;

    /// <summary>
    /// The routing keys this module reads by <b>its own cursor</b> (see
    /// <see cref="IntegrationEventSubscriptionsBuilder.OnFanOut{TEvent,THandler}"/>), grouped by
    /// the producer schema their first segment names. Disjoint from <see cref="RoutingKeysBySchema"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> FanOutRoutingKeysBySchema =>
        _fanOutRoutingKeys
            .GroupBy(key => key[..key.IndexOf('.')])
            .ToDictionary(group => group.Key, group => group.Order(StringComparer.Ordinal).ToArray());

    /// <summary>
    /// Which producer schemas' <c>outbox_messages</c> tables the polling consumer reads, and for
    /// which routing keys. By default a key is polled from the schema its first segment names
    /// (<c>trips.trip-completed</c> → <c>trips</c>) — the producer's own namespace. A subscription
    /// made with <see cref="IntegrationEventSubscriptionsBuilder.OnProducedBy{TEvent,THandler}"/>
    /// is polled from the schemas it names instead, for a contract event that lives in the
    /// <em>consumer's</em> namespace but is raised by several producers (Compliance's
    /// record-produced event, written by Drivers, Fleet and Trips).
    /// </summary>
    public IReadOnlyDictionary<string, string[]> RoutingKeysBySchema
    {
        get
        {
            var bySchema = new Dictionary<string, List<string>>();
            foreach (var routingKey in _eventTypesByRoutingKey.Keys.Where(key => !_fanOutRoutingKeys.Contains(key)))
            {
                var schemas = _producerSchemasByRoutingKey.TryGetValue(routingKey, out var explicitSchemas)
                    ? explicitSchemas
                    : [routingKey[..routingKey.IndexOf('.')]];

                foreach (var schema in schemas)
                {
                    if (!bySchema.TryGetValue(schema, out var keys))
                    {
                        keys = [];
                        bySchema[schema] = keys;
                    }

                    if (!keys.Contains(routingKey))
                    {
                        keys.Add(routingKey);
                    }
                }
            }

            return bySchema.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        }
    }
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

    /// <summary>
    /// Like <see cref="On{TEvent,THandler}"/>, but tells the outbox-polling consumer which
    /// producer schemas to read the event from instead of the routing key's own first segment.
    /// For an event declared in the consumer's namespace and raised by other modules — the
    /// default derivation would poll only the consumer's own outbox and never see a producer's
    /// row. Schemas are lowercase module names (<c>"drivers"</c>, <c>"fleet"</c>) and must exist:
    /// polling a schema with no <c>outbox_messages</c> table fails every tick. Irrelevant to the
    /// RabbitMQ consumer, which binds by routing key.
    /// </summary>
    public IntegrationEventSubscriptionsBuilder OnProducedBy<TEvent, THandler>(params string[] producerSchemas)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        if (producerSchemas.Length == 0)
        {
            throw new ArgumentException("Name at least one producer schema, or use On<,>() for the default derivation.", nameof(producerSchemas));
        }

        _services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        _subscriptions.AddProducedBy(typeof(TEvent), producerSchemas);
        return this;
    }

    /// <summary>
    /// Like <see cref="On{TEvent,THandler}"/>, but for an event <b>another module already
    /// consumes</b>. The status-tracked path keeps one <c>processing_status</c> per outbox row, so
    /// the first consumer to process a row marks it Processed and every other consumer of the same
    /// routing key silently never sees it — two status subscribers to <c>fleet.vehicle-changed</c>
    /// would each receive roughly half the vehicles. A fan-out subscription instead reads the
    /// producer's rows past <b>this module's own cursor</b> (one row per routing key in the
    /// consumer's <c>projection_checkpoints</c>, named <c>inbox:&lt;routing-key&gt;</c>) and never
    /// reads or writes <c>processing_status</c>, so it is invisible to every other consumer.
    /// <para>
    /// A new fan-out subscription starts at position 0 — the routing key's whole history replays,
    /// which is what builds a replica. A row whose handler keeps failing is retried with backoff,
    /// then logged and skipped after <see cref="OutboxPollingOptions.MaxAttempts"/> (there is no
    /// per-consumer Failed state to park it in); reserve this mode for full-snapshot "changed"
    /// events, where the next change heals a skipped one.
    /// </para>
    /// </summary>
    public IntegrationEventSubscriptionsBuilder OnFanOut<TEvent, THandler>()
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        _services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        _subscriptions.AddFanOut(typeof(TEvent));
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
