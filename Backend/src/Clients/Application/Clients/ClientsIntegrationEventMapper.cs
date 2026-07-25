using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Clients;
using NorthernLink.Shared.Kernel;
using NorthernLink.Clients.Domain.Clients;
using NorthernLink.Clients.Domain.Clients.Events;
using NorthernLink.Clients.Domain.Contracts;
using NorthernLink.Clients.Domain.Contracts.Events;

namespace NorthernLink.Clients.Application.Clients;

/// <summary>
/// Clients' explicit domain-event → integration-event translation. Client create/update
/// both map to <c>ClientChangedIntegrationEvent</c> and every contract lifecycle event maps
/// to the full-snapshot <c>ContractChangedIntegrationEvent</c>, so consumers (Trips'
/// <c>client_lookup</c>, Billing's <c>contract_snapshots</c>) maintain replicas by upsert.
/// Purchase orders stay internal (null) — invoices snapshot the PO number as a string, so
/// no module needs a PO replica.
/// </summary>
public sealed class ClientsIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate) =>
        domainEvent switch
        {
            ClientCreatedDomainEvent or ClientUpdatedDomainEvent when aggregate is Client client =>
                new ClientChangedIntegrationEvent(
                    client.Id,
                    client.TenantId,
                    client.Name,
                    client.Type.ToString(),
                    client.ServiceType?.ToString(),
                    client.Tag),
            ContractActivatedDomainEvent or ContractUpdatedDomainEvent or ContractTerminatedDomainEvent
                when aggregate is Contract contract =>
                new ContractChangedIntegrationEvent(
                    contract.Id,
                    contract.TenantId,
                    contract.ClientId,
                    contract.ClientName,
                    contract.StartDate,
                    contract.EndDate,
                    contract.BillingModel.ToString(),
                    contract.RatePerRoundTripCad,
                    contract.GstApplicable,
                    contract.BudgetCode,
                    contract.BillingFrequency.ToString(),
                    contract.NetTermsDays,
                    contract.DefaultPoNumber,
                    contract.Status.ToString()),
            _ => null,
        };
}
