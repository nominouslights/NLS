using NorthernLink.Clients.Domain.Contracts;
using NorthernLink.Clients.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Clients.Infrastructure.Persistence.Projections;

/// <summary>
/// The single implementation of "which contract is the client's active one" for the
/// denormalized summary columns on <c>rm_clients</c> — shared by
/// <see cref="ClientProjection"/> (client journal rows) and
/// <see cref="ClientContractSummaryProjection"/> (contract journal rows) so the two can
/// never disagree. Picks the active contract whose inclusive period covers today; when
/// none does (e.g. a renewal signed ahead of its start), falls back to the next upcoming
/// active contract so the profile screen still shows the terms that will apply.
/// </summary>
internal static class ActiveContractFields
{
    public static void Apply(ClientReadModel row, IReadOnlyCollection<Contract> contracts)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var active = contracts
            .Where(contract => contract.Status == ContractStatus.Active && contract.Covers(today))
            .OrderByDescending(contract => contract.StartDate)
            .FirstOrDefault();

        active ??= contracts
            .Where(contract => contract.Status == ContractStatus.Active && contract.StartDate > today)
            .OrderBy(contract => contract.StartDate)
            .FirstOrDefault();

        if (active is null)
        {
            row.ActiveContractId = null;
            row.ActiveContractStartDate = null;
            row.ActiveContractEndDate = null;
            row.ActiveContractBillingModel = null;
            row.ActiveContractRatePerRoundTripCad = null;
            row.ActiveContractGstApplicable = null;
            row.ActiveContractBudgetCode = null;
            row.ActiveContractBillingFrequency = null;
            row.ActiveContractNetTermsDays = null;
            row.ActiveContractDefaultPoNumber = null;
            return;
        }

        row.ActiveContractId = active.Id;
        row.ActiveContractStartDate = active.StartDate;
        row.ActiveContractEndDate = active.EndDate;
        row.ActiveContractBillingModel = active.BillingModel.ToString();
        row.ActiveContractRatePerRoundTripCad = active.RatePerRoundTripCad;
        row.ActiveContractGstApplicable = active.GstApplicable;
        row.ActiveContractBudgetCode = active.BudgetCode;
        row.ActiveContractBillingFrequency = active.BillingFrequency.ToString();
        row.ActiveContractNetTermsDays = active.NetTermsDays;
        row.ActiveContractDefaultPoNumber = active.DefaultPoNumber;
    }
}
