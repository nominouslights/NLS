using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Projections;

/// <summary>Projects <see cref="BudgetPeriod"/> into <c>budgeting.rm_budget_periods</c>.</summary>
internal sealed class BudgetPeriodProjection : BudgetingProjection<BudgetPeriod, BudgetPeriodReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(BudgetPeriod));

    protected override void Map(BudgetPeriod source, BudgetPeriodReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.Label = source.Label;
        row.Granularity = source.Granularity.ToString();
        row.Year = source.Year;
        row.Ordinal = source.Ordinal;
        row.StartsOn = source.StartsOn;
        row.EndsOn = source.EndsOn;
        row.State = source.State.ToString();
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
