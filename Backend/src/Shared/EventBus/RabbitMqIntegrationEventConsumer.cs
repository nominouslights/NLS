using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NorthernLink.Shared.EventBus;

/// <summary>
/// Drains one module's integration-event queue. Declares the module's durable queue
/// bound to the platform topic exchange, consumes with manual ack, deserializes by
/// routing key, and invokes every registered <see cref="IIntegrationEventHandler{TEvent}"/>
/// in a fresh DI scope. On handler failure: bounded in-process retries, then a nack
/// without requeue dead-letters the message to <c>northernlink.&lt;module&gt;.dead-letter</c>
/// (via the default exchange, so one module's poison never bleeds into another's queue).
/// A down broker is logged and reconnected with backoff — it never kills the host, the
/// same tolerance philosophy as <see cref="RabbitMqInitializer"/>.
/// </summary>
public sealed class RabbitMqIntegrationEventConsumer(
    IntegrationEventSubscriptions subscriptions,
    RabbitMqOptions options,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqIntegrationEventConsumer> logger) : BackgroundService
{
    private const int MaxHandlerAttempts = 3;
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeUntilDisconnected(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Consumer for queue {Queue} lost its broker connection; reconnecting in {Delay}s",
                    subscriptions.QueueName, ReconnectDelay.TotalSeconds);
            }

            try
            {
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ConsumeUntilDisconnected(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
        };

        var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using (connection)
        {
            var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
            await using (channel)
            {
                await DeclareTopology(channel, stoppingToken);
                await channel.BasicQosAsync(0, prefetchCount: 10, global: false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, delivery) => HandleDelivery(channel, delivery, stoppingToken);
                await channel.BasicConsumeAsync(subscriptions.QueueName, autoAck: false, consumer, stoppingToken);

                logger.LogInformation(
                    "Consuming queue {Queue} bound to {RoutingKeys}",
                    subscriptions.QueueName, string.Join(", ", subscriptions.RoutingKeys));

                while (!stoppingToken.IsCancellationRequested && connection.IsOpen)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
    }

    private async Task DeclareTopology(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            exchange: options.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: subscriptions.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        // Dead-letter via the default exchange straight to the module's own DLQ by name —
        // no shared dead-letter exchange for other modules' poison to bleed through.
        await channel.QueueDeclareAsync(
            queue: subscriptions.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = subscriptions.DeadLetterQueueName,
            },
            cancellationToken: cancellationToken);

        foreach (var routingKey in subscriptions.RoutingKeys)
        {
            await channel.QueueBindAsync(
                subscriptions.QueueName, options.ExchangeName, routingKey,
                arguments: null, cancellationToken: cancellationToken);
        }
    }

    private async Task HandleDelivery(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        CancellationToken cancellationToken)
    {
        var routingKey = delivery.RoutingKey;
        var eventType = subscriptions.EventTypeFor(routingKey);
        if (eventType is null)
        {
            // Stale binding or topology drift — not this consumer's event; drop it rather
            // than poison-loop a message no handler will ever exist for.
            logger.LogWarning(
                "Queue {Queue} received unsubscribed routing key {RoutingKey}; acking and dropping",
                subscriptions.QueueName, routingKey);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
            return;
        }

        IIntegrationEvent integrationEvent;
        try
        {
            integrationEvent = (IIntegrationEvent)JsonSerializer.Deserialize(
                delivery.Body.Span, eventType, IntegrationEventJson.Options)!;
        }
        catch (JsonException exception)
        {
            logger.LogError(
                exception,
                "Queue {Queue}: payload for {RoutingKey} is not valid {EventType} JSON; dead-lettering",
                subscriptions.QueueName, routingKey, eventType.Name);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken);
            return;
        }

        for (var attempt = 1; attempt <= MaxHandlerAttempts; attempt++)
        {
            try
            {
                await InvokeHandlers(eventType, integrationEvent, cancellationToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
                return;
            }
            catch (Exception exception) when (attempt < MaxHandlerAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Handler attempt {Attempt}/{MaxAttempts} failed for {EventType} ({EventId}) on queue {Queue}",
                    attempt, MaxHandlerAttempts, eventType.Name, integrationEvent.EventId, subscriptions.QueueName);
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "All {MaxAttempts} handler attempts failed for {EventType} ({EventId}) on queue {Queue}; dead-lettering to {DeadLetterQueue}",
                    MaxHandlerAttempts, eventType.Name, integrationEvent.EventId, subscriptions.QueueName, subscriptions.DeadLetterQueueName);
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken);
                return;
            }
        }
    }

    private async Task InvokeHandlers(
        Type eventType,
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        // Same dynamic dispatch as the in-process Sender: resolve every registered
        // handler for the concrete event type and run them all.
        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
        foreach (var handler in scope.ServiceProvider.GetServices(handlerType))
        {
            await ((dynamic)handler!).Handle((dynamic)integrationEvent, cancellationToken);
        }
    }
}
