using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.Hosting;
using NorthernLink.Shared.Persistence;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Tenancy;
using Npgsql;

namespace NorthernLink.Shared.EventBus;

/// <summary>
/// Delivers integration events to a consuming module by polling the producer modules'
/// <c>outbox_messages</c> tables directly — the storing/projecting delivery path, where the
/// database is the transport and <see cref="OutboxMessage.ProcessingStatus"/> is the delivery
/// state. Chain-reaction events that must trigger commands in another module go through
/// RabbitMQ instead (<see cref="BusPublicationRegistry"/>).
///
/// Structurally mirrors <c>ProjectionWorker</c>: a delay before the first poll, a pinned
/// connection opted into the outbox tables' system RLS policy via <c>app.is_system</c>, and
/// failures logged and retried next poll — never killing the host. Producer schemas are
/// derived from the subscriptions' routing keys (first segment), so a module only ever reads
/// the Shared-owned outbox contract, never another module's domain model or DbContext.
///
/// Semantics: per-schema in position order; the status write happens after handler success in
/// its own statement, so delivery is at-least-once and handlers must be idempotent keyed on
/// the event id — the same contract as the bus path. A failing row retries with exponential
/// backoff and blocks only its own schema (ordering holds); after
/// <see cref="OutboxPollingOptions.MaxAttempts"/> it is parked as Failed and later rows flow.
/// A row whose routing key has no subscription anywhere stays Pending forever — subscribing
/// to it later auto-replays that key's entire history through the new handler.
///
/// Single API instance assumed (same as <c>OutboxDispatcher</c>): two instances would
/// double-process, which idempotent handlers absorb but logs would show. FOR UPDATE SKIP
/// LOCKED is deliberately omitted until scale-out is real.
/// </summary>
public sealed class OutboxPollingConsumer<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IntegrationEventSubscriptions subscriptions,
    OutboxPollingOptions options,
    ILogger<OutboxPollingConsumer<TDbContext>> logger) : BackgroundService
    where TDbContext : ModuleDbContext
{
    // Schema names come from our own routing-key convention (IntegrationEvents.<Domain>
    // namespaces), but they are interpolated into SQL, so validate defensively anyway.
    private readonly IReadOnlyDictionary<string, string[]> _routingKeysBySchema =
        GroupBySchema(subscriptions.RoutingKeys);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backoff = new PollBackoff(
            options.PollInterval, TimeSpan.FromSeconds(options.MaxPollBackoffSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            // Delay BEFORE the first poll — same boot reasoning as ProjectionWorker: all
            // hosted services start together and there is nothing to gain from racing the
            // first read against a cold database. After a failure this is the backed-off
            // delay instead of the interval, which matters most here: at a 1s interval this
            // is the noisiest worker on the platform when the database is unreachable.
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
                await ProcessOnceAsync(stoppingToken);

                if (backoff.RecordSuccess() is { } recoveredAfter)
                {
                    logger.LogInformation(
                        "Outbox polling for {Module} ({DbContext}) recovered after {Failures} consecutive failures",
                        subscriptions.ModuleName, typeof(TDbContext).Name, recoveredAfter);
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
                    "Outbox polling for {Module} ({DbContext}) failed {Failures}x consecutively; retrying in {RetryDelay}",
                    subscriptions.ModuleName, typeof(TDbContext).Name, failure.ConsecutiveFailures, failure.RetryDelay);
            }
        }
    }

    /// <summary>
    /// One poll: for each producer schema, read the pending subscribed rows in position order
    /// and run them through the module's handlers. Exposed for tests, which drive it directly
    /// rather than waiting on the timer.
    /// </summary>
    internal async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        // Pin one connection for the whole poll and opt into the outbox tables' system RLS
        // policy — the read and the status writes span tenants and schemas.
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.is_system', 'true', false);", cancellationToken);

            foreach (var (schema, routingKeys) in _routingKeysBySchema)
            {
                await ProcessSchemaAsync(context, schema, routingKeys, cancellationToken);
            }
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    // EF1002: the only interpolated fragment in these raw statements is the schema name,
    // which cannot be a SQL parameter and is validated against ^[a-z_]+$ in GroupBySchema
    // (it originates from our own routing-key namespace convention). Everything user- or
    // event-derived goes through real parameters.
#pragma warning disable EF1002
    private async Task ProcessSchemaAsync(
        TDbContext context,
        string schema,
        string[] routingKeys,
        CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters: the tenant filter would silently return zero rows on a system
        // session. AsNoTracking: these entities are mapped to the CONSUMER's own outbox
        // table, so tracked changes would save to the wrong schema — status writes go
        // through schema-qualified SQL below, never SaveChanges.
        var rows = await context.Set<OutboxMessage>()
            .FromSqlRaw(
                $"""
                SELECT * FROM {schema}.outbox_messages
                WHERE processing_status = 'Pending' AND routing_key = ANY(@keys)
                ORDER BY position
                LIMIT {options.BatchSize}
                """,
                new NpgsqlParameter("keys", routingKeys))
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var row in rows)
        {
            // A row in backoff blocks its whole schema for this tick so position order
            // holds; bounded by MaxAttempts, after which the row parks and later rows flow.
            if (row.ProcessingNextAttemptAtUtc is { } gate && gate > DateTimeOffset.UtcNow)
            {
                return;
            }

            var eventType = subscriptions.EventTypeFor(row.RoutingKey)!; // filtered by the query
            IIntegrationEvent integrationEvent;
            try
            {
                integrationEvent = (IIntegrationEvent)JsonSerializer.Deserialize(
                    row.Payload, eventType, IntegrationEventJson.Options)!;
            }
            catch (JsonException exception)
            {
                // Permanently broken payload — retrying can't fix it; park immediately.
                logger.LogError(
                    exception,
                    "Outbox row {Position} in {Schema} ({RoutingKey}, event {EventId}) is not valid {EventType} JSON; marking Failed",
                    row.Position, schema, row.RoutingKey, row.Id, eventType.Name);
                await MarkFailedAsync(context, schema, row.Position, "Payload deserialization failed", cancellationToken);
                continue;
            }

            try
            {
                await InvokeHandlers(row.TenantId, eventType, integrationEvent, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var attempts = row.ProcessingAttempts + 1;
                if (attempts >= options.MaxAttempts)
                {
                    logger.LogError(
                        exception,
                        "Outbox row {Position} in {Schema} ({RoutingKey}, event {EventId}) exhausted {MaxAttempts} attempts; parking as Failed",
                        row.Position, schema, row.RoutingKey, row.Id, options.MaxAttempts);
                    await MarkFailedAsync(context, schema, row.Position, exception.Message, cancellationToken);
                    continue;
                }

                logger.LogWarning(
                    exception,
                    "Outbox row {Position} in {Schema} ({RoutingKey}, event {EventId}) failed handler attempt {Attempt}",
                    row.Position, schema, row.RoutingKey, row.Id, attempts);
                var backoffSeconds = Math.Min(options.MaxBackoffSeconds, Math.Pow(2, attempts));
                await context.Database.ExecuteSqlRawAsync(
                    $$"""
                    UPDATE {{schema}}.outbox_messages
                    SET processing_attempts = {0}, processing_last_error = {1}, processing_next_attempt_at_utc = {2}
                    WHERE position = {3}
                    """,
                    [attempts, Truncate(exception.Message), DateTimeOffset.UtcNow.AddSeconds(backoffSeconds), row.Position],
                    cancellationToken);
                return; // in-backoff head blocks the schema until its gate passes
            }

            await context.Database.ExecuteSqlRawAsync(
                $$"""
                UPDATE {{schema}}.outbox_messages
                SET processing_status = 'Processed', processed_at_utc = now(), processing_last_error = NULL
                WHERE position = {0}
                """,
                [row.Position],
                cancellationToken);
            processed++;
        }

        if (processed > 0)
        {
            logger.LogInformation(
                "{Module} processed {Count} outbox event(s) from {Schema}",
                subscriptions.ModuleName, processed, schema);
        }
    }

    private async Task MarkFailedAsync(
        TDbContext context,
        string schema,
        long position,
        string error,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlRawAsync(
            $$"""
            UPDATE {{schema}}.outbox_messages
            SET processing_status = 'Failed', processing_last_error = {0}
            WHERE position = {1}
            """,
            [Truncate(error), position],
            cancellationToken);
    }
#pragma warning restore EF1002

    private async Task InvokeHandlers(
        Guid tenantId,
        Type eventType,
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        // A fresh scope per event, resolved INSIDE the ambient-tenant push (the
        // ProjectionWorker ordering): ModuleDbContext captures ITenantContext.TenantId at
        // construction, so a DbContext built before the push would carry a null tenant and
        // its query filters would silently hide every row from the handler — the handler's
        // own AmbientTenant.Push comes too late for anything constructed during resolution.
        using var scope = scopeFactory.CreateScope();
        using (AmbientTenant.Push(tenantId))
        {
            // Invocation goes through the INTERFACE method, not `dynamic`: the dynamic
            // binder binds against the handler's runtime type and silently fails on
            // implementations not visible to this assembly (e.g. non-public test doubles).
            var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
            var handleMethod = handlerType.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.Handle))!;
            foreach (var handler in scope.ServiceProvider.GetServices(handlerType))
            {
                // DoNotWrapExceptions: handler failures must reach the retry/park logic as
                // themselves, not as TargetInvocationException wrappers.
                await (Task)handleMethod.Invoke(
                    handler,
                    System.Reflection.BindingFlags.DoNotWrapExceptions,
                    binder: null,
                    [integrationEvent, cancellationToken],
                    culture: null)!;
            }
        }
    }

    private static string Truncate(string error) => error.Length <= 1024 ? error : error[..1024];

    private static IReadOnlyDictionary<string, string[]> GroupBySchema(IReadOnlyCollection<string> routingKeys)
    {
        var bySchema = routingKeys
            .GroupBy(key => key[..key.IndexOf('.')])
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (var schema in bySchema.Keys)
        {
            if (schema.Length == 0 || !schema.All(c => c is >= 'a' and <= 'z' or '_'))
            {
                throw new InvalidOperationException(
                    $"Routing-key domain segment '{schema}' is not a valid schema name.");
            }
        }

        return bySchema;
    }
}
