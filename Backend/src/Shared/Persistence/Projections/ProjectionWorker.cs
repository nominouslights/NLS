using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Shared.Persistence.Projections;

/// <summary>
/// Keeps a module's read-side materialized views fresh, and dispatches its same-module
/// secondary commands, by polling the module's append-only <c>event_journal</c> (by the
/// journal's monotonic <c>Position</c> cursor). One instance per module (registered as
/// <c>AddProjections&lt;FleetDbContext&gt;(...)</c> in the module's DI extension).
///
/// Structurally mirrors <see cref="OutboxDispatcher{TDbContext}"/>: a delay before the
/// first poll, a pinned connection opted into the tables' system RLS policy via
/// <c>app.is_system</c>, and failures logged and retried next poll — never killing the host.
/// A crash between refresh and checkpoint just re-refreshes next poll (refresh is idempotent),
/// and secondary commands are idempotent under at-least-once, so re-dispatch is safe.
///
/// Refreshes run under <c>SET ROLE northernlink_projector</c> because REFRESH needs matview
/// ownership (the app role is a granted member); the first refresh of a matview created
/// <c>WITH NO DATA</c> is non-concurrent (CONCURRENTLY can't populate an unpopulated
/// matview), thereafter CONCURRENTLY (which needs the unique index each matview carries).
/// </summary>
public sealed class ProjectionWorker<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IProjectionRegistry registry,
    ProjectionOptions options,
    ILogger<ProjectionWorker<TDbContext>> logger) : BackgroundService
    where TDbContext : ModuleDbContext
{
    private const string ProjectorRole = "northernlink_projector";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Delay BEFORE the first poll — same boot-race reasoning as OutboxDispatcher:
            // every module's hosted services start together, and there is nothing to gain
            // from racing the first journal read against a cold database.
            try
            {
                await Task.Delay(options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Projection poll for {Schema} ({DbContext}) failed; retrying next interval",
                    registry.Schema, typeof(TDbContext).Name);
            }
        }
    }

    /// <summary>
    /// One poll: read the journal batch past the checkpoint, refresh the matviews the batch
    /// touched (once each), dispatch secondary commands, and advance the checkpoint. Exposed
    /// for tests, which drive it directly rather than waiting on the timer.
    /// </summary>
    internal async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        // Pin one connection for the whole poll and opt into the tables' system RLS policy,
        // so the journal read, the refreshes, and the checkpoint write all see every tenant.
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

            // Coalesce: the distinct set of matviews touched across the whole batch, each
            // refreshed at most once per poll.
            var affected = new HashSet<string>();
            foreach (var entry in batch)
            {
                affected.UnionWith(registry.MatviewsForAggregate(entry.AggregateType));
            }

            if (affected.Count > 0)
            {
                await RefreshMatviewsAsync(context, affected, cancellationToken);
            }

            await DispatchSecondaryCommandsAsync(batch, cancellationToken);

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

            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private async Task RefreshMatviewsAsync(
        TDbContext context,
        IReadOnlyCollection<string> matviews,
        CancellationToken cancellationToken)
    {
        // REFRESH needs matview ownership; the app role is a granted member of the owner.
        // Identifiers here are code-controlled (registry + schema constants), never user
        // input, so raw SQL is safe — and DDL identifiers can't be parameterized anyway. The
        // SQL is built into locals so the EF1002 interpolation analyzer stays satisfied.
        var setRole = "SET ROLE " + ProjectorRole + ";";
        await context.Database.ExecuteSqlRawAsync(setRole, cancellationToken);
        try
        {
            foreach (var matview in matviews)
            {
                var qualified = registry.Schema + "." + matview;
                var populated = await IsPopulatedAsync(context, qualified, cancellationToken);

                // CONCURRENTLY can't populate an unpopulated (WITH NO DATA) matview; the first
                // refresh is a plain blocking one, every refresh after that is concurrent.
                var concurrently = populated ? "CONCURRENTLY " : string.Empty;
                var refresh = "REFRESH MATERIALIZED VIEW " + concurrently + qualified + ";";
                await context.Database.ExecuteSqlRawAsync(refresh, cancellationToken);
            }
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("RESET ROLE;", cancellationToken);
        }
    }

    private static async Task<bool> IsPopulatedAsync(
        TDbContext context,
        string qualifiedMatview,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT relispopulated FROM pg_class WHERE oid = CAST(@matview AS regclass);";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "matview";
        parameter.Value = qualifiedMatview;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
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
