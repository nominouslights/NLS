using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Hosting;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Shared.Persistence.Projections;

/// <summary>
/// Keeps a module's read-model tables fresh, and dispatches its same-module secondary commands,
/// by polling the module's append-only <c>event_journal</c> (by the journal's monotonic
/// <c>Position</c> cursor). One instance per module (registered as
/// <c>AddProjections&lt;FleetDbContext&gt;(...)</c> in the module's DI extension).
///
/// Structurally mirrors <see cref="OutboxDispatcher{TDbContext}"/>: a delay before the first
/// poll, a pinned connection opted into the tables' system RLS policy via <c>app.is_system</c>,
/// and failures logged and retried next poll — never killing the host.
///
/// Each poll applies TARGETED upserts for only the aggregates the batch touched, rather than
/// recomputing an entire materialized view. The read rows and the checkpoint advance in a single
/// SaveChanges, so a crash mid-poll leaves the checkpoint unmoved and the batch is simply
/// re-applied — which is safe because projections are idempotent.
/// </summary>
public sealed class ProjectionWorker<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IProjectionRegistry<TDbContext> registry,
    ProjectionOptions options,
    ILogger<ProjectionWorker<TDbContext>> logger) : BackgroundService
    where TDbContext : ModuleDbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backoff = new PollBackoff(
            options.PollInterval, TimeSpan.FromSeconds(options.MaxPollBackoffSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            // Delay BEFORE the first poll — same boot-race reasoning as OutboxDispatcher:
            // every module's hosted services start together, and there is nothing to gain
            // from racing the first journal read against a cold database. After a failure
            // this is the backed-off delay instead of the interval.
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
                        "Projection poll for {Schema} ({DbContext}) recovered after {Failures} consecutive failures",
                        registry.Schema, typeof(TDbContext).Name, recoveredAfter);
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
                    "Projection poll for {Schema} ({DbContext}) failed {Failures}x consecutively; retrying in {RetryDelay}",
                    registry.Schema, typeof(TDbContext).Name, failure.ConsecutiveFailures, failure.RetryDelay);
            }
        }
    }

    /// <summary>
    /// One poll: read the journal batch past the checkpoint, apply the projections for every
    /// aggregate the batch touched (once each), dispatch secondary commands, and advance the
    /// checkpoint. Exposed for tests, which drive it directly rather than waiting on the timer.
    /// </summary>
    internal async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        // Pin one connection for the whole poll and opt into the tables' system RLS policy, so
        // the journal read, the read-model writes, and the checkpoint write all span tenants.
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.is_system', 'true', false);", cancellationToken);

            var checkpoint = await context.Set<ProjectionCheckpoint>()
                .FirstOrDefaultAsync(c => c.ProjectionName == registry.Schema, cancellationToken);
            var lastPosition = checkpoint?.LastPosition ?? 0;

            var batch = await context.Set<EventJournalEntry>()
                .IgnoreQueryFilters()
                .Where(entry => entry.Position > lastPosition)
                .OrderBy(entry => entry.Position)
                .Take(options.BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                return;
            }

            // Coalesce: the distinct aggregates touched across the whole batch, each projected
            // at most once per poll no matter how many events it produced.
            var touched = new HashSet<(string AggregateType, Guid AggregateId)>();
            foreach (var entry in batch)
            {
                touched.Add((entry.AggregateType, entry.AggregateId));
            }

            foreach (var (aggregateType, aggregateId) in touched)
            {
                foreach (var projection in registry.ProjectionsForAggregate(aggregateType))
                {
                    await projection.ApplyAsync(context, aggregateId, cancellationToken);
                }
            }

            var maxPosition = batch[^1].Position; // ordered ascending
            if (checkpoint is null)
            {
                context.Set<ProjectionCheckpoint>().Add(new ProjectionCheckpoint
                {
                    ProjectionName = registry.Schema,
                    LastPosition = maxPosition,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                });
            }
            else
            {
                checkpoint.LastPosition = maxPosition;
                checkpoint.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }

            // Read rows + checkpoint commit together: the projection is never "applied but
            // uncheckpointed" in a way that could be lost, and re-application is idempotent.
            await context.SaveChangesAsync(cancellationToken);

            await DispatchSecondaryCommandsAsync(batch, cancellationToken);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private async Task DispatchSecondaryCommandsAsync(
        IReadOnlyList<EventJournalEntry> batch,
        CancellationToken cancellationToken)
    {
        foreach (var entry in batch)
        {
            var command = registry.CreateCommand(entry);
            if (command is null)
            {
                continue;
            }

            // A fresh scope (its own DbContext + connection) so the command runs on a
            // tenant-scoped session, not the poll's pinned system connection. The ambient
            // tenant makes TenantSessionInterceptor set app.tenant_id from the journal row.
            using var commandScope = scopeFactory.CreateScope();
            var sender = commandScope.ServiceProvider.GetRequiredService<ISender>();

            using (AmbientTenant.Push(entry.TenantId))
            {
                var result = await sender.Send(command, cancellationToken);
                if (result.IsFailure)
                {
                    logger.LogWarning(
                        "Projection {Schema}: secondary command for {EventType} on {AggregateId} failed: {Error}",
                        registry.Schema, entry.EventType, entry.AggregateId, result.Error.Code);
                }
            }
        }
    }
}
