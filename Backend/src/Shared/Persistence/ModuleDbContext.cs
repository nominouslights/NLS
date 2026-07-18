using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
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
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        ClearDomainEvents();
        return result;
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        AppendAuditEntries();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        ClearDomainEvents();
        return result;
    }

    /// <summary>
    /// The audit pipeline. Runs before base.SaveChanges so every row it adds commits in
    /// the same implicit transaction as the aggregate state — the whole atomicity
    /// guarantee. Saves that touch no aggregates (e.g. the outbox dispatcher marking
    /// rows dispatched) fall through untouched.
    /// </summary>
    private void AppendAuditEntries()
    {
        StampTenantId();

        // Materialize before appending audit rows below mutates the tracker.
        var aggregates = ChangeTracker.Entries<AggregateRoot>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (aggregates.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var correlationId = CurrentCorrelationId();

        foreach (var entry in aggregates)
        {
            var aggregate = entry.Entity;

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
