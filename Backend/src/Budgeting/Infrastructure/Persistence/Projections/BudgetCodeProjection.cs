using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Projections;

/// <summary>Projects <see cref="BudgetCode"/> into <c>budgeting.rm_budget_codes</c>.</summary>
internal sealed class BudgetCodeProjection : BudgetingProjection<BudgetCode, BudgetCodeReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(BudgetCode));

    protected override void Map(BudgetCode source, BudgetCodeReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.Code = source.Code;
        row.Name = source.Name;
        row.Description = source.Description;
        row.Category = source.Category.ToString();
        row.ServiceLine = source.ServiceLine?.ToString();
        row.CostCentre = source.CostCentre;
        row.ParentCodeId = source.ParentCodeId;
        row.GlAccountCode = source.GlAccountCode;
        row.TaxTreatment = source.TaxTreatment?.ToString();
        row.BudgetOwnerUserId = source.BudgetOwnerUserId;
        row.ReviewFrequency = source.ReviewFrequency.ToString();
        row.IsActive = source.IsActive;
        row.CreatedBy = source.CreatedBy;
        row.ModifiedBy = source.ModifiedBy;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
