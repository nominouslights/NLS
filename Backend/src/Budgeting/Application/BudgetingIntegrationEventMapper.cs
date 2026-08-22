using NorthernLink.Shared.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Application;

/// <summary>
/// Budgeting's explicit domain-event → integration-event translation. Everything stays
/// internal (null) today: budget periods are this module's private state, and no other
/// module maintains a replica. The class exists so the module's save pipeline has its
/// mapper seam ready the day a public contract appears (e.g. a period-locked event for
/// accounting sync).
/// </summary>
public sealed class BudgetingIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate) => null;
}
