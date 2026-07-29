using NorthernLink.Shared.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Notifications.Application;

/// <summary>
/// Notifications' explicit domain-event → integration-event translation. Everything stays
/// internal (null) today: templates and dispatch history are this module's private state,
/// and no other module maintains a replica of either. The class exists so the module's
/// save pipeline has its mapper seam ready the day a public contract appears (e.g. a
/// dispatch-recorded event for a future audit feed).
/// </summary>
public sealed class NotificationsIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate) => null;
}
