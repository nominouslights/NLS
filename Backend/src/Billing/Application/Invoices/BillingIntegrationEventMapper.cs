using NorthernLink.Shared.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Application.Invoices;

/// <summary>
/// Billing's explicit domain-event → integration-event translation. Nothing is public
/// contract today — no other module reacts to invoicing — so every event maps to null.
/// Extending Billing's public surface means adding a case here plus an event record in
/// NorthernLink.Shared/IntegrationEvents/Billing/ — never auto-publishing.
/// </summary>
public sealed class BillingIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate) => null;
}
