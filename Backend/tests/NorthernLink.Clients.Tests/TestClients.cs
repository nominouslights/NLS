using NorthernLink.Clients.Domain.Clients;
using NorthernLink.Clients.Domain.Contracts;
using Xunit;

namespace NorthernLink.Clients.Tests;

/// <summary>Factory helpers to build clients and contracts in any state.</summary>
internal static class TestClients
{
    public static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static Client Create(
        string name = "Vale Manitoba Operations",
        ClientType type = ClientType.Client,
        ClientServiceType serviceType = ClientServiceType.ContractCrew,
        string tag = "Corporate",
        string? agreementReference = null,
        string? notes = null)
    {
        var result = Client.Create(TenantId, name, type, serviceType, tag, agreementReference, notes);
        Assert.True(result.IsSuccess, $"Test client creation failed: {result.Error.Code}");
        return result.Value;
    }

    public static Contract CreateContract(
        Guid? clientId = null,
        string clientName = "Vale Manitoba Operations",
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        BillingModel billingModel = BillingModel.RoundTripRate,
        decimal? ratePerRoundTripCad = 1450m,
        bool gstApplicable = true,
        string? budgetCode = "ZBB-CREW-01",
        BillingFrequency billingFrequency = BillingFrequency.Monthly,
        int netTermsDays = 30,
        string? defaultPoNumber = "PO-88231")
    {
        var result = Contract.Create(
            TenantId,
            clientId ?? Guid.NewGuid(),
            clientName,
            startDate ?? new DateOnly(2026, 1, 1),
            endDate,
            billingModel,
            ratePerRoundTripCad,
            gstApplicable,
            budgetCode,
            billingFrequency,
            netTermsDays,
            defaultPoNumber);

        Assert.True(result.IsSuccess, $"Test contract creation failed: {result.Error.Code}");
        return result.Value;
    }
}
