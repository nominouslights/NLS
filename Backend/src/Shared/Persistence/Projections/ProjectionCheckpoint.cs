namespace NorthernLink.Shared.Persistence.Projections;

/// <summary>
/// One row per module's projection worker, recording how far it has consumed that module's
/// append-only <c>event_journal</c> (by the journal's DB-generated monotonic
/// <c>Position</c> cursor). System-owned and spans tenants — deliberately no
/// <c>tenant_id</c>: the worker refreshes every tenant's rows in one pass under a system
/// session, so the cursor is a single module-wide value. Lives in the module's own schema
/// (<c>projection_checkpoints</c>), registered centrally from
/// <see cref="Auditing"/>-style config in <c>ModuleDbContext</c>.
/// </summary>
public sealed class ProjectionCheckpoint
{
    /// <summary>Stable per-worker key (the module schema name, e.g. "fleet").</summary>
    public required string ProjectionName { get; init; }

    /// <summary>Highest <c>event_journal.position</c> the worker has processed.</summary>
    public long LastPosition { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
