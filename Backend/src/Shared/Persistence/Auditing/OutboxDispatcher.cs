using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.Hosting;

namespace NorthernLink.Shared.Persistence.Auditing;

/// <summary>
/// Publishes a module's pending CHAIN-REACTION outbox rows to RabbitMQ — only routing keys
/// designated in <see cref="BusPublicationRegistry"/>; storing/projecting events are
/// consumed in-database by <c>OutboxPollingConsumer</c> and never touch the bus. With an
/// empty registry (the current state) the dispatcher idles without querying. One instance
/// per module (registered as <c>AddHostedService&lt;OutboxDispatcher&lt;FleetDbContext&gt;&gt;()</c>
/// in the module's DI extension). Delivery is at-least-once: a crash between publish and
/// mark-dispatched causes a redelivery, so consumers dedupe on the event id.
///
/// The dispatcher has no tenant, so it opts into the outbox tables' system RLS policy by
/// setting <c>app.is_system</c> on its pinned connection — request-path sessions never set
/// it, so tenant isolation is unaffected. Broker or database failures are logged and
/// retried next poll; they never kill the host (same tolerance philosophy as
/// <see cref="RabbitMqInitializer"/>). Single API instance today — scale-out later needs
/// FOR UPDATE SKIP LOCKED, deliberately omitted until then.
/// </summary>
public sealed class OutboxDispatcher<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IOutboxTransport transport,
    BusPublicationRegistry registry,
    OutboxOptions options,
    ILogger<OutboxDispatcher<TDbContext>> logger) : BackgroundService
    where TDbContext : ModuleDbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backoff = new PollBackoff(
            options.PollInterval, TimeSpan.FromSeconds(options.MaxPollBackoffSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            // Delay BEFORE the first poll, not after — one interval of head start so
            // consumers declare and bind their durable queues before the first publish.
            // The old silent-loss consequence of losing that race is gone (publishes are
            // now mandatory + confirmed, so an unroutable message throws and retries),
            // but the head start still spares the first events a pointless failed attempt.
            // After a failure this is the backed-off delay instead of the interval.
            try
            {
                await Task.Delay(backoff.NextDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await DispatchPendingAsync(stoppingToken);

                if (backoff.RecordSuccess() is { } recoveredAfter)
                {
                    logger.LogInformation(
                        "Outbox poll for {DbContext} recovered after {Failures} consecutive failures",
                        typeof(TDbContext).Name, recoveredAfter);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                var failure = backoff.RecordFailure();
                logger.Log(
                    failure.Level,
                    exception,
                    "Outbox poll for {DbContext} failed {Failures}x consecutively; retrying in {RetryDelay}",
                    typeof(TDbContext).Name, failure.ConsecutiveFailures, failure.RetryDelay);
            }
        }
    }

    private async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        if (registry.RoutingKeys.Count == 0)
        {
            // Nothing is designated for the bus — idle without touching the database.
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        // Pin one connection for the iteration and opt into the system RLS policy.
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.is_system', 'true', false);", cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var pending = await context.Set<OutboxMessage>()
                .IgnoreQueryFilters()
                .Where(m => m.DispatchedAtUtc == null
                    && registry.RoutingKeys.Contains(m.RoutingKey)
                    && m.Attempts < options.MaxAttempts
                    && (m.NextAttemptAtUtc == null || m.NextAttemptAtUtc <= now))
                .OrderBy(m => m.Position)
                .Take(options.BatchSize)
                .ToListAsync(cancellationToken);

            if (pending.Count == 0)
            {
                return;
            }

            foreach (var message in pending)
            {
                try
                {
                    var body = Encoding.UTF8.GetBytes(message.Payload);
                    await transport.Publish(message.RoutingKey, body, cancellationToken);
                    message.DispatchedAtUtc = DateTimeOffset.UtcNow;
                }
                catch (Exception exception)
                {
                    message.Attempts++;
                    message.LastError = exception.Message;
                    message.NextAttemptAtUtc = DateTimeOffset.UtcNow
                        .AddSeconds(Math.Min(300, Math.Pow(2, message.Attempts)));

                    if (message.Attempts >= options.MaxAttempts)
                    {
                        logger.LogError(
                            exception,
                            "Outbox message {MessageId} ({RoutingKey}) exceeded {MaxAttempts} attempts and is parked as poison",
                            message.Id, message.RoutingKey, options.MaxAttempts);
                    }
                    else
                    {
                        logger.LogWarning(
                            exception,
                            "Outbox message {MessageId} ({RoutingKey}) failed publish attempt {Attempt}",
                            message.Id, message.RoutingKey, message.Attempts);
                    }
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
