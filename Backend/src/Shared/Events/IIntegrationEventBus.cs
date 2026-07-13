namespace NorthernLink.Shared.Events;

/// <summary>
/// Cross-module message bus (RabbitMQ). Convention: topic exchange <c>northernlink.events</c>,
/// routing key <c>&lt;module&gt;.&lt;event-name&gt;</c> (e.g. <c>trips.trip-completed</c>),
/// one queue per consuming module.
/// </summary>
public interface IIntegrationEventBus
{
    Task Publish<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}

/// <summary>
/// Handler a module registers to consume another module's integration event (via
/// <c>AddIntegrationEventConsumer</c> in the module's DI extension). Runs in its own DI
/// scope outside any HTTP request; typically injects ISender and dispatches a command.
/// Delivery is at-least-once — implementations must be idempotent keyed on the event id.
/// </summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task Handle(TEvent integrationEvent, CancellationToken cancellationToken);
}
