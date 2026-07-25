using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Billing.Application.Integration;
using NorthernLink.Shared.IntegrationEvents.Clients;
using Xunit;

namespace NorthernLink.Billing.Tests;

public class ContractChangedIntegrationEventHandlerTests
{
    private static ContractChangedIntegrationEvent Event(
        Guid contractId,
        decimal? rate = 120m,
        string status = "Active") => new(
        contractId,
        TestBilling.TenantId,
        TestBilling.ClientId,
        "Lynn Lake Mining Co.",
        new DateOnly(2026, 1, 1),
        null,
        "RoundTripRate",
        rate,
        GstApplicable: true,
        "ZBB-CREW-01",
        "Monthly",
        30,
        "PO-7781",
        status);

    [Fact]
    public async Task Handling_the_same_event_twice_upserts_one_row()
    {
        var repository = new InMemoryContractSnapshotRepository();
        var handler = new ContractChangedIntegrationEventHandler(
            repository, NullLogger<ContractChangedIntegrationEventHandler>.Instance);
        var contractId = Guid.NewGuid();
        var integrationEvent = Event(contractId);

        await handler.Handle(integrationEvent, CancellationToken.None);
        await handler.Handle(integrationEvent, CancellationToken.None);

        var snapshot = Assert.Single(repository.Snapshots);
        Assert.Equal(contractId, snapshot.Id);
        Assert.Equal(TestBilling.TenantId, snapshot.TenantId);
        Assert.Equal(120m, snapshot.RatePerRoundTripCad);
        Assert.Equal("RoundTripRate", snapshot.BillingModel);
    }

    [Fact]
    public async Task A_later_event_for_the_same_contract_updates_the_row_in_place()
    {
        var repository = new InMemoryContractSnapshotRepository();
        var handler = new ContractChangedIntegrationEventHandler(
            repository, NullLogger<ContractChangedIntegrationEventHandler>.Instance);
        var contractId = Guid.NewGuid();

        await handler.Handle(Event(contractId, rate: 120m), CancellationToken.None);
        await handler.Handle(Event(contractId, rate: 135m, status: "Terminated"), CancellationToken.None);

        var snapshot = Assert.Single(repository.Snapshots);
        Assert.Equal(135m, snapshot.RatePerRoundTripCad);
        Assert.Equal("Terminated", snapshot.Status);
    }
}
