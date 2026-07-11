using System.Text.Json;
using Microsoft.Extensions.Logging;
using NorthernLink.BuildingBlocks.Application.Events;
using RabbitMQ.Client;

namespace NorthernLink.BuildingBlocks.Infrastructure.EventBus;

/// <summary>
/// RabbitMQ-backed implementation of <see cref="IIntegrationEventBus"/>. Connects lazily
/// on first publish so the host can boot (and log a clear warning) when the broker is down,
/// instead of crash-looping. One topic exchange, routing key per <see cref="EventRoutingKey"/>.
/// </summary>
public sealed class RabbitMqIntegrationEventBus(
    RabbitMqOptions options,
    ILogger<RabbitMqIntegrationEventBus> logger) : IIntegrationEventBus, IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task Publish<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        var channel = await EnsureChannel(cancellationToken);
        var routingKey = EventRoutingKey.For(integrationEvent.GetType());
        var body = JsonSerializer.SerializeToUtf8Bytes(integrationEvent, integrationEvent.GetType(), SerializerOptions);

        await channel.BasicPublishAsync(options.ExchangeName, routingKey, body, cancellationToken);

        logger.LogInformation(
            "Published integration event {EventType} ({EventId}) with routing key {RoutingKey}",
            integrationEvent.GetType().Name, integrationEvent.EventId, routingKey);
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
