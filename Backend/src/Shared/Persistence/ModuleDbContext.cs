using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Shared.Persistence;

/// <summary>
/// Base DbContext for a domain module. Each module owns a dedicated Postgres schema
/// (the module name, lowercase) inside the single shared database — the modular-monolith
/// equivalent of database-per-service. Modules never join across schemas; cross-module
/// data flows through integration events instead.
///
/// Every module inherits the platform's audit pipeline: within the same transaction as
/// each save, the pipeline stamps tenant ids, bumps each touched aggregate's Version,
/// appends the raised domain events to <c>event_journal</c>, appends a full JSON snapshot
/// of each touched aggregate to <c>aggregate_snapshots</c> (the moment-in-time record the
/// audit viewer scrolls), and writes mapper-approved integration events to
/// <c>outbox_messages</c> for the outbox dispatcher. All three tables live in the module's
/// own schema, registered here so every module gets the identical shape.
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    /// <summary>
    /// Journal event type recorded when an aggregate is hard-deleted. Synthetic — there is no
    /// domain event for a delete — but projections need a journal row to react to, otherwise the
    /// read side keeps a row for an aggregate that no longer exists.
    /// </summary>
    public const string AggregateDeletedEventType = "aggregate-deleted";

    private readonly IIntegrationEventMapper? _integrationEventMapper;

    protected ModuleDbContext(
        DbContextOptions options,
        string schema,
        ITenantContext tenantContext,
        IIntegrationEventMapper? integrationEventMapper = null)
        : base(options)
    {
        Schema = schema;
        TenantId = tenantContext.TenantId;
        _integrationEventMapper = integrationEventMapper;
    }

    /// <summary>The Postgres schema this module's tables live in.</summary>
    protected string Schema { get; }

    /// <summary>
    /// Current tenant, captured per context instance. Query filters must reference this
    /// member (via `this`) so EF evaluates the value per instance instead of caching the
    /// first instance's tenant in the model.
    /// </summary>
    protected Guid? TenantId { get; }

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        ConfigureModule(modelBuilder);
        ConfigureAuditModel(modelBuilder);
    }

    /// <summary>The module's own entity configurations and tenant query filters.</summary>
    protected abstract void ConfigureModule(ModelBuilder modelBuilder);

    private void ConfigureAuditModel(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new EventJournalEntryConfiguration());
        modelBuilder.ApplyConfiguration(new AggregateSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        // Read-side projection checkpoint — system-owned, spans tenants (no query filter),
        // one row per module's projection worker. Applied here so every module schema gets it.
        modelBuilder.ApplyConfiguration(new ProjectionCheckpointConfiguration());

        // Tenant isolation, API half — same dual-enforcement rule as module tables.
        // The outbox dispatcher deliberately bypasses this with IgnoreQueryFilters.
        modelBuilder.Entity<EventJournalEntry>().HasQueryFilter(e => e.TenantId == TenantId);
        modelBuilder.Entity<AggregateSnapshot>().HasQueryFilter(s => s.TenantId == TenantId);
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(m => m.TenantId == TenantId);

        // Aggregate conventions, applied to every module's aggregates centrally:
        // Version as an optimistic-concurrency token (two writers can't both produce
        // version N), DomainEvents never mapped.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(AggregateRoot).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).Ignore(nameof(AggregateRoot.DomainEvents));
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(AggregateRoot.Version))
                    .HasColumnName("version")
                    .IsConcurrencyToken();
            }
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AppendAuditEntries();

        int result;
        try
        {
            result = base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateException exception) when (IsSnapshotVersionCollision(exception))
        {
            throw AsLostRace(exception);
        }

        ClearDomainEvents();
        return result;
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        AppendAuditEntries();

        int result;
        try
        {
            result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsSnapshotVersionCollision(exception))
        {
            throw AsLostRace(exception);
        }

        ClearDomainEvents();
        return result;
    }

    /// <summary>
    /// A stale writer computes the same next Version as the writer that beat it, so its
    /// <c>aggregate_snapshots</c> row collides with the winner's on the (aggregate_id, version)
    /// primary key. EF orders independent commands by table name, so that INSERT usually runs —
    /// and fails — before the aggregate's own UPDATE ever gets to report zero rows affected. The
    /// collision can only mean two writers produced the same version of one aggregate, which is
    /// exactly an optimistic-concurrency loss; the transaction has already rolled back either way.
    /// </summary>
    private bool IsSnapshotVersionCollision(DbUpdateException exception) =>
        exception is not DbUpdateConcurrencyException
        && exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && postgres.ConstraintName is { } constraint
        && constraint == Model.FindEntityType(typeof(AggregateSnapshot))?.FindPrimaryKey()?.GetName();

    /// <summary>
    /// Re-raises a snapshot version collision as the <see cref="DbUpdateConcurrencyException"/>
    /// every repository's lost-race path already catches. Its <c>Entries</c> is EMPTY: the
    /// database does not say which aggregate collided, and building the list would need EF's
    /// internal entry API. A caller that branches on <c>Entries</c> (Trips'
    /// <c>BookeoImportRepository</c>) therefore takes its generic-conflict arm — the same
    /// outcome it reached before, when this surfaced as a raw unique violation.
    /// </summary>
    private static DbUpdateConcurrencyException AsLostRace(DbUpdateException exception) =>
        new(
            "An aggregate was modified by another writer since it was loaded: this save's aggregate_snapshots row " +
            "collided with the version that writer already committed. Nothing from this save was persisted.",
            exception);

    /// <summary>
    /// The audit pipeline. Runs before base.SaveChanges so every row it adds commits in
    /// the same implicit transaction as the aggregate state — the whole atomicity
    /// guarantee. Saves that touch no aggregates (e.g. the outbox dispatcher marking
    /// rows dispatched) fall through untouched. "Touch" includes an aggregate whose only
    /// change is to an owned entity (a jsonb owned collection, an OwnsOne): its owner is
    /// promoted to Modified first, so it is versioned, snapshotted and journaled like any
    /// other write.
    /// </summary>
    private void AppendAuditEntries()
    {
        StampTenantId();

        // Entries() runs DetectChanges, so edits to owned collections are visible from here on.
        // Materialize before appending audit rows below mutates the tracker.
        var tracked = ChangeTracker.Entries().ToList();

        TouchOwnersOfChangedOwnedEntities(tracked);

        var aggregates = tracked
            .Where(e => e.Entity is AggregateRoot
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (aggregates.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var correlationId = CurrentCorrelationId();

        foreach (var entry in aggregates)
        {
            var aggregate = (AggregateRoot)entry.Entity;

            // Guard: every insert/update of an aggregate must raise at least one domain
            // event. An eventless write produces no event_journal row, so the projection
            // worker never sees it and the read model silently goes stale — this converts
            // that silent-staleness bug class into an immediate, loud failure. Deletes are
            // exempt: the synthetic aggregate-deleted journal row below covers them. A save
            // that changes ONLY an owned entity (an OwnsMany(...).ToJson(...) collection, an
            // OwnsOne) reaches this guard too: TouchOwnersOfChangedOwnedEntities has already
            // promoted its Unchanged owner to Modified, so that write can no longer slip past
            // the version check, the snapshot, the journal or this guard.
            if (entry.State is EntityState.Added or EntityState.Modified && aggregate.DomainEvents.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{aggregate.GetType().Name} (id {aggregate.Id}) was saved as {entry.State} without raising a " +
                    "domain event. An eventless write produces no event_journal row, so the projection worker never " +
                    "sees it and the read model silently goes stale — raise a domain event from the mutating method.");
            }

            // Deleted aggregates still get a final version + snapshot of their pre-delete
            // state, so hard deletes don't vanish from history.
            aggregate.IncrementVersion();

            var tenantId = aggregate is ITenantScoped scoped ? scoped.TenantId : Guid.Empty;
            var aggregateType = AuditNames.ForAggregate(aggregate.GetType());

            // Serialized after IncrementVersion so state.version matches the row's version.
            Set<AggregateSnapshot>().Add(new AggregateSnapshot
            {
                AggregateId = aggregate.Id,
                Version = aggregate.Version,
                TenantId = tenantId,
                AggregateType = aggregateType,
                State = AuditJson.Serialize(aggregate),
                CreatedAtUtc = now,
                CorrelationId = correlationId,
            });

            // A hard delete raises no domain event, so without this the journal would record
            // nothing and every read-side projection would strand a row for an aggregate that no
            // longer exists. (The old materialized-view read side hid this: a REFRESH recomputed
            // from the base tables, so deleted rows vanished implicitly.) Projections poll the
            // journal, so a deletion has to appear there like any other change.
            if (entry.State == EntityState.Deleted)
            {
                Set<EventJournalEntry>().Add(new EventJournalEntry
                {
                    EventId = Guid.NewGuid(),
                    TenantId = tenantId,
                    AggregateType = aggregateType,
                    AggregateId = aggregate.Id,
                    AggregateVersion = aggregate.Version,
                    EventType = AggregateDeletedEventType,
                    Payload = AuditJson.Serialize(aggregate),
                    OccurredAtUtc = now,
                    RecordedAtUtc = now,
                    CorrelationId = correlationId,
                });
            }

            foreach (var domainEvent in aggregate.DomainEvents)
            {
                Set<EventJournalEntry>().Add(new EventJournalEntry
                {
                    EventId = Guid.NewGuid(),
                    TenantId = tenantId,
                    AggregateType = aggregateType,
                    AggregateId = aggregate.Id,
                    AggregateVersion = aggregate.Version,
                    EventType = AuditNames.ForEvent(domainEvent.GetType()),
                    Payload = AuditJson.Serialize(domainEvent),
                    OccurredAtUtc = domainEvent.OccurredAtUtc,
                    RecordedAtUtc = now,
                    CorrelationId = correlationId,
                });

                if (_integrationEventMapper?.Map(domainEvent, aggregate) is { } integrationEvent)
                {
                    Set<OutboxMessage>().Add(new OutboxMessage
                    {
                        Id = integrationEvent.EventId,
                        TenantId = tenantId,
                        EventType = integrationEvent.GetType().Name,
                        RoutingKey = EventRoutingKey.For(integrationEvent.GetType()),
                        Payload = IntegrationEventJson.Serialize(integrationEvent),
                        OccurredAtUtc = integrationEvent.OccurredAtUtc,
                        CreatedAtUtc = now,
                    });
                }
            }
        }
    }

    /// <summary>
    /// Promotes the root owner of every changed owned entity to Modified when that owner is an
    /// <see cref="AggregateRoot"/> EF still considers Unchanged.
    ///
    /// EF tracks owned types as entities of their own. When a save changes ONLY an owned
    /// collection mapped with <c>OwnsMany(...).ToJson(...)</c> (or an <c>OwnsOne</c>), the owned
    /// entries go Added / Modified / Deleted while the owning aggregate stays Unchanged — yet EF
    /// still rewrites the owner's row (the jsonb column, or the table-split columns). Left alone,
    /// that write would skip everything the pipeline guarantees: no Version bump and so no
    /// optimistic-concurrency check (a stale writer silently wins), no snapshot, no journal row
    /// (the read model never learns), no outbox row, and the eventless-write guard never fires.
    ///
    /// Only the Version property is marked modified — that is enough to move the owner to
    /// Modified (EF then puts <c>version = @original</c> in the UPDATE's WHERE clause and writes
    /// the incremented value), without forcing every scalar column into the SET list.
    /// Ownership can nest (an owned type inside a jsonb collection item), so each changed owned
    /// entry is walked up through its ownership chain to the first non-owned entity.
    /// </summary>
    private static void TouchOwnersOfChangedOwnedEntities(IReadOnlyList<EntityEntry> tracked)
    {
        // Fast path: nothing to promote unless some aggregate is Unchanged AND some owned entry
        // changed. Bulk inserts (every aggregate Added) never build the index below.
        if (!tracked.Any(e => e.Entity is AggregateRoot && e.State == EntityState.Unchanged))
        {
            return;
        }

        var changedOwned = tracked
            .Where(e => e.Metadata.IsOwned()
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (changedOwned.Count == 0)
        {
            return;
        }

        // Owned entries are found by key, not by navigation: a Deleted owned item is no longer
        // reachable from its owner's collection, but its key still holds the owner's key.
        var byPrimaryKey = new Dictionary<TrackedKey, EntityEntry>();
        foreach (var entry in tracked)
        {
            if (entry.Metadata.FindPrimaryKey() is { } primaryKey)
            {
                byPrimaryKey.TryAdd(
                    new TrackedKey(entry.Metadata.GetRootType(), ValuesOf(entry, primaryKey.Properties)),
                    entry);
            }
        }

        foreach (var owned in changedOwned)
        {
            if (FindRootOwner(owned, byPrimaryKey) is { Entity: AggregateRoot, State: EntityState.Unchanged } owner)
            {
                owner.Property(nameof(AggregateRoot.Version)).IsModified = true;
            }
        }
    }

    private static EntityEntry? FindRootOwner(EntityEntry owned, Dictionary<TrackedKey, EntityEntry> byPrimaryKey)
    {
        var current = owned;

        while (current.Metadata.FindOwnership() is { } ownership)
        {
            var principalKey = new TrackedKey(
                ownership.PrincipalEntityType.GetRootType(),
                ValuesOf(current, ownership.Properties));

            if (!byPrimaryKey.TryGetValue(principalKey, out var principal))
            {
                return null;
            }

            current = principal;
        }

        return current;
    }

    private static object?[] ValuesOf(EntityEntry entry, IReadOnlyList<IProperty> properties)
    {
        var values = new object?[properties.Count];
        for (var i = 0; i < properties.Count; i++)
        {
            values[i] = entry.Property(properties[i].Name).CurrentValue;
        }

        return values;
    }

    /// <summary>An entity's identity in the tracker: its hierarchy root type plus key values.</summary>
    private sealed class TrackedKey(IEntityType rootType, object?[] values) : IEquatable<TrackedKey>
    {
        private readonly IEntityType _rootType = rootType;
        private readonly object?[] _values = values;

        public bool Equals(TrackedKey? other) =>
            other is not null
            && ReferenceEquals(_rootType, other._rootType)
            && _values.SequenceEqual(other._values);

        public override bool Equals(object? obj) => Equals(obj as TrackedKey);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(_rootType);
            foreach (var value in _values)
            {
                hash.Add(value);
            }

            return hash.ToHashCode();
        }
    }

    private void StampTenantId()
    {
        if (TenantId is not { } tenantId)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Property(nameof(ITenantScoped.TenantId)).CurrentValue = tenantId;
            }
        }
    }

    private void ClearDomainEvents()
    {
        foreach (var entry in ChangeTracker.Entries<AggregateRoot>())
        {
            entry.Entity.ClearDomainEvents();
        }
    }

    /// <summary>
    /// The current request's W3C trace id, reinterpreted as a Guid — a free,
    /// request-scoped correlation value until a richer command context exists.
    /// </summary>
    private static Guid? CurrentCorrelationId()
    {
        if (Activity.Current is not { } activity)
        {
            return null;
        }

        Span<byte> bytes = stackalloc byte[16];
        activity.TraceId.CopyTo(bytes);
        return new Guid(bytes);
    }
}
