namespace ShuttleApi.Domain.Common;

public interface IAggregateRoot
{
    IReadOnlyList<DomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
    object? GetId();
}
