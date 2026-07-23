namespace NorthernLink.Shared.Persistence.Projections;

/// <summary>
/// Projects one write-side aggregate into its read-model table. Implementations are stateless
/// (the context is passed per call) and are held as singletons by the module's registry.
///
/// Replaces the previous materialized-view design: Postgres cannot attach RLS policies to a
/// matview, which forced a separate owning role plus a security-barrier wrapper view. An
/// ordinary table carries the same native RLS policy as every other table on the platform, so
/// the projector role — and its whole class of "role must already exist" migration-ordering
/// bugs — disappears.
///
/// Both methods must be IDEMPOTENT: the projection worker is at-least-once (a crash between
/// applying and checkpointing re-applies the same journal batch), so an upsert must converge on
/// the same row rather than duplicating it.
/// </summary>
public interface IProjection<in TDbContext>
    where TDbContext : ModuleDbContext
{
    /// <summary>
    /// The aggregate type this projection reacts to, as the stable kebab-cased name stored in
    /// <c>event_journal.aggregate_type</c> (see <c>AuditNames.ForAggregate</c>). More than one
    /// projection may share an aggregate type — retirement certificates are written during a
    /// vehicle's retirement and so ride the "vehicle" journal.
    /// </summary>
    string AggregateType { get; }

    /// <summary>
    /// Upserts the read row(s) for a single aggregate instance. A source aggregate that no
    /// longer exists deletes its read row, so hard deletes propagate. Changes are tracked on
    /// <paramref name="context"/>; the worker commits them with the checkpoint in one save.
    /// </summary>
    Task ApplyAsync(TDbContext context, Guid aggregateId, CancellationToken cancellationToken);

    /// <summary>
    /// Rebuilds every read row for this projection from the write tables — the recovery path
    /// that <c>REFRESH MATERIALIZED VIEW</c> used to provide implicitly. Used by
    /// <see cref="ProjectionRebuilder{TDbContext}"/> after drift, a bad deploy, or when a new
    /// read model is introduced.
    /// </summary>
    Task RebuildAllAsync(TDbContext context, CancellationToken cancellationToken);
}
