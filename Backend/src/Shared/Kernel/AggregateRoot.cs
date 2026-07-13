namespace NorthernLink.Shared.Kernel;

/// <summary>
/// Root of an aggregate — the only entity type repositories load and save directly.
/// Collects domain events raised during a unit of work for dispatch after persistence.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Monotonic per-aggregate version, incremented by the persistence pipeline on every
    /// save and stamped on the audit snapshot/journal rows written in the same transaction.
    /// Also mapped as an optimistic-concurrency token, so two writers can never both
    /// produce the same version. Internal setter: only the save pipeline may bump it.
    /// </summary>
    public int Version { get; private set; }

    internal void IncrementVersion() => Version++;

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
