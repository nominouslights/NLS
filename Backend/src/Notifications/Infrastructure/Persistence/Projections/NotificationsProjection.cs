using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Persistence.Projections;

namespace NorthernLink.Notifications.Infrastructure.Persistence.Projections;

/// <summary>
/// Shared upsert / delete / rebuild mechanics for the common case: one write-side aggregate row
/// projects to exactly one read-model row carrying the same id. Subclasses supply only the
/// aggregate name and the field mapping. (The Notifications copy of Fleet's FleetProjection —
/// each module owns its base because IProjection is generic over the module's DbContext.)
///
/// Every query here uses <c>IgnoreQueryFilters()</c>. The projection worker runs with no ambient
/// tenant, so the tenant query filter would compare against a null TenantId and match nothing.
/// Spanning tenants is intentional and is exactly what the worker's pinned <c>app.is_system</c>
/// session authorises — the read tables' RLS policy admits it explicitly.
///
/// Nothing is saved here: the worker commits the tracked changes together with the checkpoint in
/// one <c>SaveChangesAsync</c>, so a poll is all-or-nothing.
/// </summary>
internal abstract class NotificationsProjection<TSource, TRead> : IProjection<NotificationsDbContext>
    where TSource : Entity
    where TRead : class, new()
{
    public abstract string AggregateType { get; }

    /// <summary>Copies the aggregate's state onto the read row, including the id.</summary>
    protected abstract void Map(TSource source, TRead row);

    public async Task ApplyAsync(NotificationsDbContext context, Guid aggregateId, CancellationToken cancellationToken)
    {
        var source = await context.Set<TSource>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == aggregateId, cancellationToken);

        var row = await FindRowAsync(context, aggregateId, cancellationToken);

        // Source gone (hard delete) — the read row goes with it.
        if (source is null)
        {
            if (row is not null)
            {
                context.Set<TRead>().Remove(row);
            }

            return;
        }

        if (row is null)
        {
            row = new TRead();
            Map(source, row);
            context.Set<TRead>().Add(row);
        }
        else
        {
            Map(source, row);
        }
    }

    public async Task RebuildAllAsync(NotificationsDbContext context, CancellationToken cancellationToken)
    {
        var sources = await context.Set<TSource>().IgnoreQueryFilters().ToListAsync(cancellationToken);
        var rows = await context.Set<TRead>().IgnoreQueryFilters().ToListAsync(cancellationToken);

        var rowsById = rows.ToDictionary(row => IdOf(context, row));
        var seen = new HashSet<Guid>();

        foreach (var source in sources)
        {
            seen.Add(source.Id);

            if (rowsById.TryGetValue(source.Id, out var existing))
            {
                Map(source, existing);
            }
            else
            {
                var fresh = new TRead();
                Map(source, fresh);
                context.Set<TRead>().Add(fresh);
            }
        }

        // Orphans: read rows whose source aggregate no longer exists.
        foreach (var (id, row) in rowsById)
        {
            if (!seen.Contains(id))
            {
                context.Set<TRead>().Remove(row);
            }
        }
    }

    // EF.Property rather than a typed lambda: TRead is only constrained to `class, new()`, so
    // there is no compile-time Id member to reference.
    private static Task<TRead?> FindRowAsync(NotificationsDbContext context, Guid id, CancellationToken cancellationToken) =>
        context.Set<TRead>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(row => EF.Property<Guid>(row, "Id") == id, cancellationToken);

    private static Guid IdOf(NotificationsDbContext context, TRead row) =>
        (Guid)context.Entry(row).Property("Id").CurrentValue!;
}
