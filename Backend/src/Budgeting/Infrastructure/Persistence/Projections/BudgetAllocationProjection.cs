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
        row.Title = source.Title;
        row.AmountCad = source.AmountCad;
        row.Quantity = source.Quantity;
        row.UnitCostCad = source.UnitCostCad;
        row.Unit = source.Unit;
        row.Justification = source.Justification;
        row.SpendType = source.SpendType.ToString();
        row.Recurrence = source.Recurrence.ToString();
        row.Vendor = source.Vendor;
        // A fresh list, never the aggregate's own: the read row must not alias write-side state.
        row.Tags = [.. source.Tags];
        row.Priority = source.Priority.ToString();
        row.Assumptions = source.Assumptions;
        row.ConsequenceIfUnfunded = source.ConsequenceIfUnfunded;
        row.CreatedBy = source.CreatedBy;
        row.ModifiedBy = source.ModifiedBy;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
