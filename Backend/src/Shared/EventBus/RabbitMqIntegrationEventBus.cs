using System.Text.Json;
using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using RabbitMQ.Client;

namespace NorthernLink.Shared.EventBus;

/// <summary>
/// RabbitMQ-backed implementation of <see cref="IIntegrationEventBus"/>. Connects lazily
/// on first publish so the host can boot (and log a clear warning) when the broker is down,
/// instead of crash-looping. One topic exchange, routing key per <see cref="EventRoutingKey"/>.
/// </summary>
public sealed class RabbitMqIntegrationEventBus(
    RabbitMqOptions options,
    ILogger<RabbitMqIntegrationEventBus> logger) : IIntegrationEventBus, IOutboxTransport, IAsyncDisposable
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task Publish<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        var routingKey = EventRoutingKey.For(integrationEvent.GetType());
        var body = JsonSerializer.SerializeToUtf8Bytes(
            integrationEvent, integrationEvent.GetType(), IntegrationEventJson.Options);

        await Publish(routingKey, body, cancellationToken);

        logger.LogInformation(
            "Published integration event {EventType} ({EventId}) with routing key {RoutingKey}",
            integrationEvent.GetType().Name, integrationEvent.EventId, routingKey);
    }

    /// <summary>Raw publish used by the outbox dispatcher — the payload is already wire JSON.</summary>
    public async Task Publish(string routingKey, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        var channel = await EnsureChannel(cancellationToken);
        await channel.BasicPublishAsync(options.ExchangeName, routingKey, body, cancellationToken);
    }

    /// <summary>
    /// Opens the connection and declares the exchange. Called eagerly at startup by
    /// <see cref="RabbitMqInitializer"/> (failure tolerated) and lazily on publish (failure thrown).
    /// </summary>
    public async Task<IChannel> EnsureChannel(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            var factory = new ConnectionFactory
            {
                HostName = options.HostName,
                Port = options.Port,
                UserName = options.UserName,
                Password = options.Password,
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await _channel.ExchangeDeclareAsync(
                exchange: options.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Connected to RabbitMQ at {Host}:{Port}; exchange '{Exchange}' declared",
                options.HostName, options.Port, options.ExchangeName);

            return _channel;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _connectionLock.Dispose();
    }
}
