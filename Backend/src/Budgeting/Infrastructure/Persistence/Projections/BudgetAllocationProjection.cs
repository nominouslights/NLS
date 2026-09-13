using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Projections;

/// <summary>Projects <see cref="BudgetAllocation"/> into <c>budgeting.rm_budget_allocations</c>.</summary>
internal sealed class BudgetAllocationProjection : BudgetingProjection<BudgetAllocation, BudgetAllocationReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(BudgetAllocation));

    protected override void Map(BudgetAllocation source, BudgetAllocationReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.PeriodId = source.PeriodId;
        row.BudgetCodeId = source.BudgetCodeId;
        row.Code = source.Code;
        row.AmountCad = source.AmountCad;
        row.Justification = source.Justification;
        row.CreatedBy = source.CreatedBy;
        row.ModifiedBy = source.ModifiedBy;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
