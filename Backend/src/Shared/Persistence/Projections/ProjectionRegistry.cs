using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Shared.Persistence.Projections;

/// <summary>
/// One module's projection wiring, consumed by <see cref="ProjectionWorker{TDbContext}"/>:
/// which materialized views each aggregate's journal rows refresh, and which journal event
/// types trigger a same-module secondary command. Built once at startup via
/// <see cref="ProjectionRegistryBuilder"/> (mirroring how
/// <c>AddIntegrationEventConsumer</c> builds its subscriptions). Keys use the stable
/// kebab-cased names from <see cref="AuditNames"/> — the exact strings stored in
/// <c>event_journal.aggregate_type</c> / <c>event_journal.event_type</c>.
/// </summary>
public interface IProjectionRegistry
{
    /// <summary>The module schema (e.g. "fleet"); also the projection checkpoint key.</summary>
    string Schema { get; }

    /// <summary>Every matview this module owns, in a stable order (used only for diagnostics).</summary>
    IReadOnlyCollection<string> Matviews { get; }

    /// <summary>Matviews to refresh for a journal row of the given aggregate type; empty if none.</summary>
    IReadOnlySet<string> MatviewsForAggregate(string aggregateType);

    /// <summary>
    /// The same-module secondary command for a journal row, or null when its event type has
    /// no binding. The factory receives the journal row so it can key the command on the
    /// aggregate id, tenant, and version.
    /// </summary>
    ICommand? CreateCommand(EventJournalEntry entry);
}

/// <summary>Fluent registration surface for <c>AddProjections</c>.</summary>
public sealed class ProjectionRegistryBuilder(string schema)
{
    private readonly HashSet<string> _matviews = [];
    private readonly Dictionary<string, HashSet<string>> _matviewsByAggregate = [];
    private readonly Dictionary<string, Func<EventJournalEntry, ICommand?>> _commandsByEventType = [];

    /// <summary>Declares a matview this module owns (informational; also implied by <see cref="OnAggregate"/>).</summary>
    public ProjectionRegistryBuilder Matview(string name)
    {
        _matviews.Add(name);
        return this;
    }

    /// <summary>
    /// Maps an aggregate (by its <see cref="AuditNames.ForAggregate"/> kebab name, e.g.
    /// "vehicle") to the matviews that must be refreshed when that aggregate changes.
    /// </summary>
    public ProjectionRegistryBuilder OnAggregate(string aggregateType, params string[] matviews)
    {
        if (!_matviewsByAggregate.TryGetValue(aggregateType, out var set))
        {
            set = [];
            _matviewsByAggregate[aggregateType] = set;
        }

        foreach (var matview in matviews)
        {
            set.Add(matview);
            _matviews.Add(matview);
        }

        return this;
    }

    /// <summary>
    /// Binds a same-module secondary command to a domain event type (keyed on
    /// <see cref="AuditNames.ForEvent"/>). Delivery is at-least-once — the produced command's
    /// handler must be idempotent (check-before-insert), exactly like an integration handler.
    /// </summary>
    public ProjectionRegistryBuilder OnEvent<TDomainEvent>(Func<EventJournalEntry, ICommand?> commandFactory)
    {
        _commandsByEventType[AuditNames.ForEvent(typeof(TDomainEvent))] = commandFactory;
        return this;
    }

    internal IProjectionRegistry Build() => new ProjectionRegistry(
        schema,
        _matviews,
        _matviewsByAggregate.ToDictionary(pair => pair.Key, pair => (IReadOnlySet<string>)pair.Value),
        _commandsByEventType);

    private sealed class ProjectionRegistry(
        string schema,
        IReadOnlyCollection<string> matviews,
        IReadOnlyDictionary<string, IReadOnlySet<string>> matviewsByAggregate,
        IReadOnlyDictionary<string, Func<EventJournalEntry, ICommand?>> commandsByEventType)
        : IProjectionRegistry
    {
        private static readonly IReadOnlySet<string> None = new HashSet<string>();

        public string Schema => schema;

        public IReadOnlyCollection<string> Matviews => matviews;

        public IReadOnlySet<string> MatviewsForAggregate(string aggregateType) =>
            matviewsByAggregate.TryGetValue(aggregateType, out var set) ? set : None;

        public ICommand? CreateCommand(EventJournalEntry entry) =>
            commandsByEventType.TryGetValue(entry.EventType, out var factory) ? factory(entry) : null;
    }
}
