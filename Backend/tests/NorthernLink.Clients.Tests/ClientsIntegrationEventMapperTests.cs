using NorthernLink.Shared.IntegrationEvents.Clients;
using NorthernLink.Clients.Application.Clients;
using NorthernLink.Clients.Domain.Clients;
using NorthernLink.Clients.Domain.Clients.Events;
using NorthernLink.Clients.Domain.Contracts;
using NorthernLink.Clients.Domain.Contracts.Events;
using Xunit;

namespace NorthernLink.Clients.Tests;

/// <summary>
/// The module's public contract: client create/update → ClientChangedIntegrationEvent,
/// every contract lifecycle event → the full-snapshot ContractChangedIntegrationEvent,
/// everything else stays internal.
/// </summary>
public class ClientsIntegrationEventMapperTests
{
    private readonly ClientsIntegrationEventMapper _mapper = new();

    [Fact]
    public void Client_created_maps_to_client_changed_snapshot()
    {
        var client = TestClients.Create(
            name: "Vale Manitoba Operations", serviceType: ClientServiceType.ContractCrew, tag: "Corporate");

        var result = _mapper.Map(new ClientCreatedDomainEvent(client.Id, client.TenantId), client);

        var integrationEvent = Assert.IsType<ClientChangedIntegrationEvent>(result);
        Assert.Equal(client.Id, integrationEvent.ClientId);
        Assert.Equal(client.TenantId, integrationEvent.TenantId);
        Assert.Equal("Vale Manitoba Operations", integrationEvent.Name);
        Assert.Equal("Client", integrationEvent.Type);
        Assert.Equal("ContractCrew", integrationEvent.ServiceType);
        Assert.Equal("Corporate", integrationEvent.Tag);
    }

    [Fact]
    public void Client_updated_maps_to_client_changed_with_current_state()
    {
        var client = TestClients.Create();
        Assert.True(client.Update("Snow Lake Wellness", ClientServiceType.Community, "Program", null, null).IsSuccess);

        var result = _mapper.Map(new ClientUpdatedDomainEvent(client.Id, client.TenantId), client);

        var integrationEvent = Assert.IsType<ClientChangedIntegrationEvent>(result);
        Assert.Equal("Snow Lake Wellness", integrationEvent.Name);
        Assert.Equal("Client", integrationEvent.Type);
        Assert.Equal("Community", integrationEvent.ServiceType);
        Assert.Equal("Program", integrationEvent.Tag);
    }

    [Fact]
    public void Contract_activated_maps_to_full_contract_snapshot()
    {
        var clientId = Guid.NewGuid();
        var contract = TestClients.CreateContract(
            clientId: clientId,
            clientName: "Vale Manitoba Operations",
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 12, 31));

        var result = _mapper.Map(new ContractActivatedDomainEvent(contract.Id, contract.TenantId), contract);

        var integrationEvent = Assert.IsType<ContractChangedIntegrationEvent>(result);
        Assert.Equal(contract.Id, integrationEvent.ContractId);
        Assert.Equal(contract.TenantId, integrationEvent.TenantId);
        Assert.Equal(clientId, integrationEvent.ClientId);
        Assert.Equal("Vale Manitoba Operations", integrationEvent.ClientName);
        Assert.Equal(new DateOnly(2026, 1, 1), integrationEvent.StartDate);
        Assert.Equal(new DateOnly(2026, 12, 31), integrationEvent.EndDate);
        Assert.Equal("RoundTripRate", integrationEvent.BillingModel);
        Assert.Equal(1450m, integrationEvent.RatePerRoundTripCad);
        Assert.True(integrationEvent.GstApplicable);
        Assert.Equal("ZBB-CREW-01", integrationEvent.BudgetCode);
        Assert.Equal("Monthly", integrationEvent.BillingFrequency);
        Assert.Equal(30, integrationEvent.NetTermsDays);
        Assert.Equal("PO-88231", integrationEvent.DefaultPoNumber);
        Assert.Equal("Active", integrationEvent.Status);
    }

    [Fact]
    public void Contract_updated_maps_with_amended_terms()
    {
        var contract = TestClients.CreateContract();
        Assert.True(contract.Update(
            new DateOnly(2026, 2, 1),
            null,
            BillingModel.Manual,
            null,
            gstApplicable: false,
            "ZBB-CHTR-03",
            BillingFrequency.Weekly,
            45,
            null).IsSuccess);

        var result = _mapper.Map(new ContractUpdatedDomainEvent(contract.Id, contract.TenantId), contract);

        var integrationEvent = Assert.IsType<ContractChangedIntegrationEvent>(result);
        Assert.Null(integrationEvent.EndDate);
        Assert.Equal("Manual", integrationEvent.BillingModel);
        Assert.Null(integrationEvent.RatePerRoundTripCad);
        Assert.False(integrationEvent.GstApplicable);
        Assert.Equal("ZBB-CHTR-03", integrationEvent.BudgetCode);
        Assert.Equal("Weekly", integrationEvent.BillingFrequency);
        Assert.Equal(45, integrationEvent.NetTermsDays);
        Assert.Null(integrationEvent.DefaultPoNumber);
        Assert.Equal("Active", integrationEvent.Status);
    }

    [Fact]
    public void Contract_terminated_maps_with_terminated_status()
    {
        var contract = TestClients.CreateContract();
        Assert.True(contract.Terminate().IsSuccess);

        var result = _mapper.Map(new ContractTerminatedDomainEvent(contract.Id, contract.TenantId), contract);

        var integrationEvent = Assert.IsType<ContractChangedIntegrationEvent>(result);
        Assert.Equal("Terminated", integrationEvent.Status);
    }

    [Fact]
    public void Unrelated_domain_events_stay_internal()
    {
        var client = TestClients.Create();

        Assert.Null(_mapper.Map(new UnrelatedDomainEvent(), client));
    }

    private sealed record UnrelatedDomainEvent : NorthernLink.Shared.Kernel.IDomainEvent
    {
        public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    }
}
