using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Shared.Persistence.Projections;

/// <summary>
/// One module's projection wiring, consumed by <see cref="ProjectionWorker{TDbContext}"/>:
/// which <see cref="IProjection{TDbContext}"/> each aggregate's journal rows drive, and which
/// journal event types trigger a same-module secondary command. Built once at startup via
/// <see cref="ProjectionRegistryBuilder{TDbContext}"/> (mirroring how
/// <c>AddIntegrationEventConsumer</c> builds its subscriptions). Keys use the stable
/// kebab-cased names from <see cref="AuditNames"/> — the exact strings stored in
/// <c>event_journal.aggregate_type</c> / <c>event_journal.event_type</c>.
/// </summary>
public interface IProjectionRegistry<TDbContext>
    where TDbContext : ModuleDbContext
{
    /// <summary>The module schema (e.g. "fleet"); also the projection checkpoint key.</summary>
    string Schema { get; }

    /// <summary>Every projection this module owns, in registration order (used by the rebuilder).</summary>
    IReadOnlyCollection<IProjection<TDbContext>> Projections { get; }

    /// <summary>Projections to apply for a journal row of the given aggregate type; empty if none.</summary>
    IReadOnlyCollection<IProjection<TDbContext>> ProjectionsForAggregate(string aggregateType);

    /// <summary>
    /// The same-module secondary command for a journal row, or null when its event type has
    /// no binding. The factory receives the journal row so it can key the command on the
    /// aggregate id, tenant, and version.
    /// </summary>
    ICommand? CreateCommand(EventJournalEntry entry);
}

/// <summary>Fluent registration surface for <c>AddProjections</c>.</summary>
public sealed class ProjectionRegistryBuilder<TDbContext>(string schema)
    where TDbContext : ModuleDbContext
{
    private readonly List<IProjection<TDbContext>> _projections = [];
    private readonly Dictionary<string, List<IProjection<TDbContext>>> _byAggregate = [];
    private readonly Dictionary<string, Func<EventJournalEntry, ICommand?>> _commandsByEventType = [];

    /// <summary>
    /// Registers a projection, keyed by its own <see cref="IProjection{TDbContext}.AggregateType"/>.
    /// Projections are stateless and held as singletons.
    /// </summary>
    public ProjectionRegistryBuilder<TDbContext> Project(IProjection<TDbContext> projection)
    {
        _projections.Add(projection);

        if (!_byAggregate.TryGetValue(projection.AggregateType, out var list))
        {
            list = [];
            _byAggregate[projection.AggregateType] = list;
        }

        list.Add(projection);
        return this;
    }

    /// <summary>
    /// Binds a same-module secondary command to a domain event type (keyed on
    /// <see cref="AuditNames.ForEvent"/>). Delivery is at-least-once — the produced command's
    /// handler must be idempotent (check-before-insert), exactly like an integration handler.
    /// </summary>
    public ProjectionRegistryBuilder<TDbContext> OnEvent<TDomainEvent>(Func<EventJournalEntry, ICommand?> commandFactory)
    {
        _commandsByEventType[AuditNames.ForEvent(typeof(TDomainEvent))] = commandFactory;
        return this;
    }

    internal IProjectionRegistry<TDbContext> Build() => new ProjectionRegistry(
        schema,
        _projections,
        _byAggregate.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyCollection<IProjection<TDbContext>>)pair.Value),
        _commandsByEventType);

    private sealed class ProjectionRegistry(
        string schema,
        IReadOnlyCollection<IProjection<TDbContext>> projections,
        IReadOnlyDictionary<string, IReadOnlyCollection<IProjection<TDbContext>>> byAggregate,
        IReadOnlyDictionary<string, Func<EventJournalEntry, ICommand?>> commandsByEventType)
        : IProjectionRegistry<TDbContext>
    {
        private static readonly IReadOnlyCollection<IProjection<TDbContext>> None = [];

        public string Schema => schema;

        public IReadOnlyCollection<IProjection<TDbContext>> Projections => projections;

        public IReadOnlyCollection<IProjection<TDbContext>> ProjectionsForAggregate(string aggregateType) =>
            byAggregate.TryGetValue(aggregateType, out var list) ? list : None;

        public ICommand? CreateCommand(EventJournalEntry entry) =>
            commandsByEventType.TryGetValue(entry.EventType, out var factory) ? factory(entry) : null;
    }
}
