using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Budgeting.Domain.CostCentres;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Projections;

/// <summary>Projects <see cref="CostCentre"/> into <c>budgeting.rm_cost_centres</c>.</summary>
internal sealed class CostCentreProjection : BudgetingProjection<CostCentre, CostCentreReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(CostCentre));

    protected override void Map(CostCentre source, CostCentreReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.Code = source.Code;
        row.Name = source.Name;
        row.Description = source.Description;
        row.OwnerUserId = source.OwnerUserId;
        row.ParentId = source.ParentId;
        row.IsActive = source.IsActive;
        row.CreatedBy = source.CreatedBy;
        row.ModifiedBy = source.ModifiedBy;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
