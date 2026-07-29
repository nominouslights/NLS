namespace NorthernLink.Shared.EventBus;

/// <summary>
/// The routing keys that <c>OutboxDispatcher</c> still publishes to RabbitMQ — the
/// chain-reaction path, for events that must trigger commands in another module
/// (handler-to-handler communication). Storing/projecting events are consumed in-database
/// by <see cref="OutboxPollingConsumer{TDbContext}"/> and never touch the bus; today that
/// is every event, so the registry is registered empty and the dispatcher idles.
///
/// Rule when adding an entry: designate only brand-new event types. The dispatcher no
/// longer marks non-bus rows dispatched, so designating an EXISTING routing key would
/// publish its entire undispatched history to the bus — months-old events firing commands.
/// If an existing key must move to the bus, first backfill
/// <c>dispatched_at_utc = now()</c> for that key.
/// </summary>
public sealed class BusPublicationRegistry
{
    private readonly HashSet<string> _routingKeys;

    /// <summary>Keys derive from the event types via <see cref="EventRoutingKey"/> so designation can't drift from the wire convention.</summary>
    public BusPublicationRegistry(params Type[] busPublishedEventTypes)
    {
        _routingKeys = [.. busPublishedEventTypes.Select(EventRoutingKey.For)];
    }

    public IReadOnlySet<string> RoutingKeys => _routingKeys;
}
