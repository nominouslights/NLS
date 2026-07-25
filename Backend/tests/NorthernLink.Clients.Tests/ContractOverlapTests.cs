using NorthernLink.Clients.Application.Contracts.Create;
using NorthernLink.Clients.Application.Contracts.Update;
using NorthernLink.Clients.Domain.Clients;
using NorthernLink.Clients.Domain.Contracts;
using Xunit;

namespace NorthernLink.Clients.Tests;

/// <summary>
/// The one-active-contract-per-client rule: periods are StartDate..EndDate inclusive,
/// null EndDate is evergreen (open-ended), any intersection with another active contract
/// is an overlap, and ended/terminated contracts never block.
/// </summary>
public class ContractOverlapTests
{
    private readonly InMemoryClientRepository _clients = new();
    private readonly InMemoryContractRepository _contracts = new();
    private readonly Client _client;

    public ContractOverlapTests()
    {
        _client = TestClients.Create();
        _clients.Add(_client);
    }

    private CreateContractCommand Command(
        DateOnly start,
        DateOnly? end,
        Guid? clientId = null) => new(
        TestClients.TenantId,
        clientId ?? _client.Id,
        start,
        end,
        BillingModel.RoundTripRate,
        1450m,
        GstApplicable: true,
        "ZBB-CREW-01",
        BillingFrequency.Monthly,
        NetTermsDays: 30,
        "PO-88231");

    private void SeedContract(DateOnly start, DateOnly? end, bool terminated = false)
    {
        var contract = TestClients.CreateContract(clientId: _client.Id, startDate: start, endDate: end);
        if (terminated)
        {
            Assert.True(contract.Terminate().IsSuccess);
        }

        _contracts.Add(contract);
    }

    private Task<NorthernLink.Shared.Kernel.Result<Guid>> Create(DateOnly start, DateOnly? end) =>
        new CreateContractCommandHandler(_clients, _contracts).Handle(Command(start, end), CancellationToken.None);

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    [Fact]
    public async Task Sequential_renewal_starting_the_day_after_is_allowed()
    {
        SeedContract(D(2026, 1, 1), D(2026, 6, 30));

        var result = await Create(D(2026, 7, 1), D(2026, 12, 31));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _contracts.Contracts.Count);
    }

    [Fact]
    public async Task Same_day_boundary_counts_as_overlap()
    {
        // Existing ends June 30 inclusive; a new contract starting June 30 shares that day.
        SeedContract(D(2026, 1, 1), D(2026, 6, 30));

        var result = await Create(D(2026, 6, 30), D(2026, 12, 31));

        Assert.True(result.IsFailure);
        Assert.Equal(ContractErrors.OverlappingPeriod, result.Error);
    }

    [Fact]
    public async Task Period_inside_an_existing_contract_is_rejected()
    {
        SeedContract(D(2026, 1, 1), D(2026, 12, 31));

        var result = await Create(D(2026, 3, 1), D(2026, 4, 30));

        Assert.True(result.IsFailure);
        Assert.Equal(ContractErrors.OverlappingPeriod, result.Error);
    }

    [Fact]
    public async Task Period_entirely_before_an_existing_contract_is_allowed()
    {
        SeedContract(D(2026, 7, 1), D(2026, 12, 31));

        var result = await Create(D(2026, 1, 1), D(2026, 6, 30));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Existing_evergreen_blocks_any_later_period()
    {
        SeedContract(D(2026, 1, 1), end: null);

        var result = await Create(D(2027, 5, 1), D(2027, 12, 31));

        Assert.True(result.IsFailure);
        Assert.Equal(ContractErrors.OverlappingPeriod, result.Error);
    }

    [Fact]
    public async Task Bounded_period_entirely_before_an_evergreen_start_is_allowed()
    {
        SeedContract(D(2026, 7, 1), end: null);

        var result = await Create(D(2026, 1, 1), D(2026, 6, 30));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Bounded_period_touching_an_evergreen_start_day_is_rejected()
    {
        SeedContract(D(2026, 7, 1), end: null);

        var result = await Create(D(2026, 1, 1), D(2026, 7, 1));

        Assert.True(result.IsFailure);
        Assert.Equal(ContractErrors.OverlappingPeriod, result.Error);
    }

    [Fact]
    public async Task New_evergreen_overlapping_a_bounded_contract_is_rejected()
    {
        SeedContract(D(2026, 1, 1), D(2026, 6, 30));

        var result = await Create(D(2026, 6, 1), end: null);

        Assert.True(result.IsFailure);
        Assert.Equal(ContractErrors.OverlappingPeriod, result.Error);
    }

    [Fact]
    public async Task New_evergreen_starting_after_a_bounded_contract_ends_is_allowed()
    {
        SeedContract(D(2026, 1, 1), D(2026, 6, 30));

        var result = await Create(D(2026, 7, 1), end: null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Two_evergreens_always_overlap()
    {
        SeedContract(D(2026, 1, 1), end: null);

        var result = await Create(D(2030, 1, 1), end: null);

        Assert.True(result.IsFailure);
        Assert.Equal(ContractErrors.OverlappingPeriod, result.Error);
    }

    [Fact]
    public async Task Terminated_contract_does_not_block_a_new_one()
    {
        SeedContract(D(2026, 1, 1), end: null, terminated: true);

        var result = await Create(D(2026, 3, 1), D(2026, 12, 31));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Other_clients_contracts_do_not_interfere()
    {
        var otherClient = TestClients.Create(name: "Snow Lake Wellness Program");
        _clients.Add(otherClient);
        _contracts.Add(TestClients.CreateContract(clientId: otherClient.Id, startDate: D(2026, 1, 1)));

        var result = await Create(D(2026, 2, 1), D(2026, 12, 31));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Unknown_client_is_not_found()
    {
        var result = await new CreateContractCommandHandler(_clients, _contracts)
            .Handle(Command(D(2026, 1, 1), null, clientId: Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ClientErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Update_overlapping_a_sibling_contract_is_rejected()
    {
        SeedContract(D(2026, 1, 1), D(2026, 6, 30));
        var renewal = TestClients.CreateContract(
            clientId: _client.Id, startDate: D(2026, 7, 1), endDate: D(2026, 12, 31));
        _contracts.Add(renewal);

        var command = new UpdateContractCommand(
            TestClients.TenantId,
            renewal.Id,
            D(2026, 6, 15), // now reaches back into the first contract
            D(2026, 12, 31),
            BillingModel.RoundTripRate,
            1450m,
            GstApplicable: true,
            "ZBB-CREW-01",
            BillingFrequency.Monthly,
            NetTermsDays: 30,
            "PO-88231");

        var result = await new UpdateContractCommandHandler(_contracts).Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ContractErrors.OverlappingPeriod, result.Error);
    }

    [Fact]
    public async Task Update_excludes_the_contract_itself_from_the_overlap_check()
    {
        var contract = TestClients.CreateContract(
            clientId: _client.Id, startDate: D(2026, 1, 1), endDate: D(2026, 6, 30));
        _contracts.Add(contract);

        // New period intersects the contract's own current period — that must be fine.
        var command = new UpdateContractCommand(
            TestClients.TenantId,
            contract.Id,
            D(2026, 2, 1),
            D(2026, 9, 30),
            BillingModel.RoundTripRate,
            1500m,
            GstApplicable: true,
            "ZBB-CREW-01",
            BillingFrequency.Monthly,
            NetTermsDays: 30,
            "PO-88231");

        var result = await new UpdateContractCommandHandler(_contracts).Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 9, 30), contract.EndDate);
        Assert.Equal(1500m, contract.RatePerRoundTripCad);
    }
}
