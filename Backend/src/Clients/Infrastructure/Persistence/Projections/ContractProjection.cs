using NorthernLink.Clients.Domain.Contracts;
using NorthernLink.Clients.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Clients.Infrastructure.Persistence.Projections;

/// <summary>Projects <see cref="Contract"/> into <c>clients.rm_contracts</c>.</summary>
internal sealed class ContractProjection : ClientsProjection<Contract, ContractReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(Contract));

    protected override void Map(Contract source, ContractReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.ClientId = source.ClientId;
        row.ClientName = source.ClientName;
        row.StartDate = source.StartDate;
        row.EndDate = source.EndDate;
        row.BillingModel = source.BillingModel.ToString();
        row.RatePerRoundTripCad = source.RatePerRoundTripCad;
        row.GstApplicable = source.GstApplicable;
        row.BudgetCode = source.BudgetCode;
        row.BillingFrequency = source.BillingFrequency.ToString();
        row.NetTermsDays = source.NetTermsDays;
        row.DefaultPoNumber = source.DefaultPoNumber;
        row.Status = source.Status.ToString();
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
