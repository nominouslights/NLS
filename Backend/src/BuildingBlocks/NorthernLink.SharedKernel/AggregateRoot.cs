namespace NorthernLink.SharedKernel;

/// <summary>
/// Root of an aggregate — the only entity type repositories load and save directly.
/// Collects domain events raised during a unit of work for dispatch after persistence.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
