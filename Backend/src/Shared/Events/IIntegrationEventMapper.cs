using NorthernLink.Shared.Kernel;

namespace NorthernLink.Shared.Events;

/// <summary>
/// A module's explicit translation from its internal domain events to its public
/// integration events. The save pipeline calls this for every raised domain event;
/// a non-null result is written to the module's transactional outbox and published
/// to RabbitMQ. Returning null (the default posture) keeps an event internal —
/// domain events are never auto-published, because integration events are a
/// deliberate public contract, not an implementation echo.
/// </summary>
public interface IIntegrationEventMapper
{
    /// <summary>
    /// <paramref name="aggregate"/> is the raiser, for context the domain event itself
    /// doesn't carry (tenant id, identifiers) — public contracts often need what internal
    /// events can leave implicit.
    /// </summary>
    IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate);
}
