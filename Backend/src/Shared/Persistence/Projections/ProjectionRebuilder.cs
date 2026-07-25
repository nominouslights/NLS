using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Shared.Persistence.Projections;

/// <summary>
/// Reconstructs a module's read-model tables from its write tables, then parks the checkpoint at
/// the journal head so the worker resumes from "now" instead of replaying history.
///
/// WHY this exists: a materialized view was, by definition, exactly its defining query — a
/// crashed refresh simply re-ran and self-corrected. Targeted upserts trade that implicit
/// self-healing for efficiency, so drift (a missed event, a projection bug, a newly added read
/// model) needs an explicit repair path. This is it.
///
/// Deliberately manual — not run on boot. Rebuilding is O(all rows) and would make every restart
/// pay for a rare recovery.
/// </summary>
public sealed class ProjectionRebuilder<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IProjectionRegistry<TDbContext> registry,
    ILogger<ProjectionRebuilder<TDbContext>> logger)
    where TDbContext : ModuleDbContext
{
    public async Task RebuildAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

        // Same pinned system session the worker uses: rebuilding spans every tenant.
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.is_system', 'true', false);", cancellationToken);

            foreach (var projection in registry.Projections)
            {
                await projection.RebuildAllAsync(context, cancellationToken);
            }

            // Park at the journal head — the rebuild already reflects every event up to here,
            // so replaying them would be wasted work (correct, but wasted).
            var head = await context.Set<EventJournalEntry>()
                .IgnoreQueryFilters()
                .OrderByDescending(entry => entry.Position)
                .Select(entry => (long?)entry.Position)
                .FirstOrDefaultAsync(cancellationToken) ?? 0;

            var checkpoint = await context.Set<ProjectionCheckpoint>()
                .FirstOrDefaultAsync(c => c.ProjectionName == registry.Schema, cancellationToken);

            if (checkpoint is null)
            {
                context.Set<ProjectionCheckpoint>().Add(new ProjectionCheckpoint
                {
                    ProjectionName = registry.Schema,
                    LastPosition = head,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                });
            }
            else
            {
                checkpoint.LastPosition = head;
                checkpoint.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Rebuilt {Count} projection(s) for {Schema}; checkpoint parked at position {Position}",
                registry.Projections.Count, registry.Schema, head);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
