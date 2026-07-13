namespace NorthernLink.Shared.EventBus;

/// <summary>
/// The outbox dispatcher's view of the broker: publish pre-serialized bytes under a
/// precomputed routing key. Outbox rows carry the exact wire payload and routing key,
/// so dispatch never needs to resolve or deserialize CLR event types. Implemented by
/// <see cref="RabbitMqIntegrationEventBus"/>; faked in tests.
/// </summary>
public interface IOutboxTransport
{
    Task Publish(string routingKey, ReadOnlyMemory<byte> body, CancellationToken cancellationToken);
}
