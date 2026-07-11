namespace NorthernLink.BuildingBlocks.Application.Events;

/// <summary>
/// An event published by one module for consumption by other modules, carried over
/// RabbitMQ. Integration events live in a module's <c>Contracts</c> project — they are
/// the module's public surface, so they must stay small, serializable, and stable.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAtUtc { get; }
}

/// <summary>Convenience base record for integration events.</summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
