namespace NorthernLink.Shared.Events;

/// <summary>
/// An event published by one module for consumption by other modules, carried over
/// RabbitMQ. Integration events live in NorthernLink.Shared under
/// <c>IntegrationEvents/&lt;Domain&gt;/</c> — they are the module's public surface, so they
/// must stay small, serializable, and stable. Delivery is at-least-once end to end
/// (outbox → broker and broker → handler), so <see cref="EventId"/> is the consumer's
/// idempotency key: handlers must tolerate seeing the same event id twice.
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
