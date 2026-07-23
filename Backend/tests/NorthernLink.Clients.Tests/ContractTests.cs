using NorthernLink.Clients.Domain.Contracts;
using NorthernLink.Clients.Domain.Contracts.Events;
using Xunit;

namespace NorthernLink.Clients.Tests;

/// <summary>Contract factory validation and lifecycle behavior.</summary>
public class ContractTests
{
    private static NorthernLink.Shared.Kernel.Result<Contract> Create(
        BillingModel billingModel = BillingModel.RoundTripRate,
        decimal? rate = 1450m,
        int netTermsDays = 30,
        DateOnly? startDate = null,
        DateOnly? endDate = null) =>
        Contract.Create(
            TestClients.TenantId,
            Guid.NewGuid(),
            "Vale Manitoba Operations",
            startDate ?? new DateOnly(2026, 1, 1),
            endDate,
            billingModel,
            rate,
            gstApplicable: true,
            "ZBB-CREW-01",
            BillingFrequency.Monthly,
            netTermsDays,
            "PO-88231");

    [Fact]
    public void Round_trip_rate_billing_requires_a_rate()
    {
        var result = Create(BillingModel.RoundTripRate, rate: null);

        Assert.True(result.IsFailure);
        Assert.Equal(ContractErrors.RateRequired, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Round_trip_rate_must_be_positive(decimal rate)
    {
        var result = Create(BillingModel.RoundTripRate, rate: rate);

        Assert.True(result.IsFailure);
        Assert.Equal(ContractErrors.InvalidRate, result.Error);
    }

    [Fact]
    public void Manual_billing_must_not_carry_a_rate()
    {
        var result = Create(BillingModel.Manual, rate: 1450m);

        Assert.True(result.IsFailure);
        Assert.Equal(ContractErrors.RateNotAllowed, result.Error);
    }

    [Fact]
    public void Manual_billing_without_a_rate_succeeds()
    {
        var result = Create(BillingModel.Manual, rate: null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.RatePerRoundTripCad);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public void Net_terms_must_be_positive(int netTermsDays)
    {
        var result = Create(netTermsDays: netTermsDays);

        Assert.True(result.IsFailure);
        Assert.Equal(ContractErrors.InvalidNetTerms, result.Error);
    }

    [Fact]
    public void End_date_before_start_date_is_invalid()
    {
        var result = Create(startDate: new DateOnly(2026, 6, 1), endDate: new DateOnly(2026, 5, 31));

        Assert.True(result.IsFailure);
        Assert.Equal(ContractErrors.InvalidPeriod, result.Error);
    }

    [Fact]
    public void Single_day_contract_is_valid()
    {
        var result = Create(startDate: new DateOnly(2026, 6, 1), endDate: new DateOnly(2026, 6, 1));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void New_contract_is_active_and_raises_activated_event()
    {
        var result = Create();

        Assert.True(result.IsSuccess);
        var contract = result.Value;
        Assert.Equal(ContractStatus.Active, contract.Status);
        var domainEvent = Assert.Single(contract.DomainEvents);
        var activated = Assert.IsType<ContractActivatedDomainEvent>(domainEvent);
        Assert.Equal(contract.Id, activated.ContractId);
        Assert.Equal(TestClients.TenantId, activated.TenantId);
    }

    [Fact]
    public void Terminate_moves_to_terminated_and_raises_event()
    {
        var contract = TestClients.CreateContract();
        contract.ClearDomainEvents();

        var result = contract.Terminate();

        Assert.True(result.IsSuccess);
        Assert.Equal(ContractStatus.Terminated, contract.Status);
        Assert.IsType<ContractTerminatedDomainEvent>(Assert.Single(contract.DomainEvents));
    }

    [Fact]
    public void Terminate_twice_is_a_conflict()
    {
        var contract = TestClients.CreateContract();
        Assert.True(contract.Terminate().IsSuccess);

        var result = contract.Terminate();

        Assert.True(result.IsFailure);
        Assert.Equal(ContractErrors.NotActive, result.Error);
    }

    [Fact]
    public void Terminated_contract_cannot_be_updated()
    {
        var contract = TestClients.CreateContract();
        Assert.True(contract.Terminate().IsSuccess);

        var result = contract.Update(
            new DateOnly(2026, 1, 1),
            null,
            BillingModel.Manual,
            null,
            gstApplicable: false,
            null,
            BillingFrequency.Weekly,
            30,
            null);

        Assert.True(result.IsFailure);
        Assert.Equal(ContractErrors.NotActive, result.Error);
    }

    [Fact]
    public void Update_applies_new_terms_and_raises_updated_event()
    {
        var contract = TestClients.CreateContract();
        contract.ClearDomainEvents();

        var result = contract.Update(
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 12, 31),
            BillingModel.RoundTripRate,
            1600m,
            gstApplicable: false,
            "ZBB-CREW-02",
            BillingFrequency.BiWeekly,
            45,
            "PO-90001");

        Assert.True(result.IsSuccess);
        Assert.Equal(1600m, contract.RatePerRoundTripCad);
        Assert.Equal(45, contract.NetTermsDays);
        Assert.Equal(BillingFrequency.BiWeekly, contract.BillingFrequency);
        Assert.IsType<ContractUpdatedDomainEvent>(Assert.Single(contract.DomainEvents));
    }
}
