namespace NorthernLink.BuildingBlocks.Application.Events;

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

/// <summary>Handler a module registers to consume another module's integration event.</summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task Handle(TEvent integrationEvent, CancellationToken cancellationToken);
}
