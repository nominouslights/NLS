using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NorthernLink.Shared.EventBus;

namespace NorthernLink.Shared.Persistence.Auditing;

/// <summary>
/// Publishes a module's pending outbox rows to RabbitMQ. One instance per module
/// (registered as <c>AddHostedService&lt;OutboxDispatcher&lt;FleetDbContext&gt;&gt;()</c> in the
/// module's DI extension). Delivery is at-least-once: a crash between publish and
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
    OutboxOptions options,
    ILogger<OutboxDispatcher<TDbContext>> logger) : BackgroundService
    where TDbContext : ModuleDbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox poll for {DbContext} failed; retrying next interval", typeof(TDbContext).Name);
            }

            try
            {
                await Task.Delay(options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
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
