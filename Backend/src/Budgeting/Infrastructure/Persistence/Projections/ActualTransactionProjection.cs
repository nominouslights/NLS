using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Budgeting.Domain.Actuals;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Projections;

/// <summary>Projects <see cref="ActualTransaction"/> into <c>budgeting.rm_actual_transactions</c>.</summary>
internal sealed class ActualTransactionProjection : BudgetingProjection<ActualTransaction, ActualTransactionReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(ActualTransaction));

    protected override void Map(ActualTransaction source, ActualTransactionReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.BudgetCodeId = source.BudgetCodeId;
        row.Code = source.Code;
        row.AmountCad = source.AmountCad;
        row.OccurredOn = source.OccurredOn;
        row.Description = source.Description;
        row.Source = source.Source.ToString();
        row.ExternalReference = source.ExternalReference;
        row.CreatedBy = source.CreatedBy;
        row.ModifiedBy = source.ModifiedBy;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
