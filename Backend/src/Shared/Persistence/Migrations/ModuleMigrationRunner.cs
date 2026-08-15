using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Shared.Persistence.Migrations;

/// <summary>
/// Brings every module's schema up to date at host startup — the arrangement
/// <c>Backend/docker/initdb/01-app-role.sql</c> has always described ("EF Core migrations run at
/// API startup as northernlink_app itself") but which nothing actually implemented. Each module
/// owns its schema and its own <c>__EFMigrationsHistory</c> table, so this walks the registered
/// contexts in order rather than migrating one database-wide history.
///
/// Concurrency: the whole sequence runs under a single Postgres session-level advisory lock, so
/// two pods coming up together serialize instead of racing on DDL. That is a deliberate outer
/// lock rather than a reliance on EF's per-context migration locking — one lock, one place, and
/// it covers the gap between contexts too.
///
/// Failure tolerance matches <c>OutboxDispatcher</c> and <c>RabbitMqInitializer</c>: a failure is
/// logged and recorded in <see cref="MigrationState"/>, never thrown. Crash-looping a container
/// on a migration failure destroys the evidence; leaving it up but permanently unready keeps the
/// pod diagnosable while still holding traffic back.
/// </summary>
public sealed class ModuleMigrationRunner(
    IServiceScopeFactory scopeFactory,
    MigrationOptions options,
    MigrationTargets targets,
    MigrationState state,
    ILogger<ModuleMigrationRunner> logger) : IHostedService
{
    /// <summary>
    /// Arbitrary but fixed 64-bit key identifying "the Northern Link schema migration" to
    /// <c>pg_advisory_lock</c>. Advisory locks share one namespace per database, so the value
    /// only has to be stable and unlikely to collide with anything else.
    /// </summary>
    private const long AdvisoryLockKey = 0x4E4C_4D49_4752_0001;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.RunOnStartup)
        {
            // Not an error and not a partial state: the schema is somebody else's job in this
            // environment, so readiness must not wait on it.
            logger.LogInformation(
                "Startup migrations are disabled (Migrations:RunOnStartup=false) — assuming the schema is already current.");
            state.MarkCompleted();
            return;
        }

        try
        {
            await MigrateAllAsync(cancellationToken);
            state.MarkCompleted();
        }
        catch (Exception ex)
        {
            state.MarkFailed(ex.Message);
            logger.LogError(ex,
                "Startup migrations failed — the host will stay up but will never report ready. ({Message})",
                ex.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task MigrateAllAsync(CancellationToken cancellationToken)
    {
        // A dedicated connection outside EF: session-level advisory locks live on the connection
        // that took them, and EF's pooled connections are returned between contexts.
        var connectionString = RequiredEnvironmentVariable.Get("ConnectionStrings__Postgres");

        await using var lockConnection = new NpgsqlConnection(connectionString);
        await lockConnection.OpenAsync(cancellationToken);

        logger.LogInformation("Waiting for the schema migration advisory lock…");
        await ExecuteAdvisoryLockAsync(lockConnection, "SELECT pg_advisory_lock($1);", cancellationToken);

        try
        {
            foreach (var contextType in targets.ContextTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await using var scope = scopeFactory.CreateAsyncScope();
                var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);

                var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pending.Count == 0)
                {
                    logger.LogInformation("{DbContext}: already current.", contextType.Name);
                    continue;
                }

                logger.LogInformation(
                    "{DbContext}: applying {Count} migration(s), through {Latest}.",
                    contextType.Name, pending.Count, pending[^1]);

                await context.Database.MigrateAsync(cancellationToken);
            }
        }
        finally
        {
            // Released explicitly rather than left to connection close, so the log line below is
            // an honest record that the lock is gone.
            await ExecuteAdvisoryLockAsync(lockConnection, "SELECT pg_advisory_unlock($1);", CancellationToken.None);
            logger.LogInformation("Schema migration advisory lock released.");
        }
    }

    private static async Task ExecuteAdvisoryLockAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(AdvisoryLockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
