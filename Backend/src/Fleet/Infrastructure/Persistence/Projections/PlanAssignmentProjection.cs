using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Fleet.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="PlanAssignment"/> into <c>fleet.rm_pm_plan_assignments</c>. Unassignment
/// is a hard delete of the aggregate row, which the base projection mirrors by removing the
/// read row.
/// </summary>
internal sealed class PlanAssignmentProjection : FleetProjection<PlanAssignment, PlanAssignmentReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(PlanAssignment));

    protected override void Map(PlanAssignment source, PlanAssignmentReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.VehicleId = source.VehicleId;
        row.PlanId = source.PlanId;
        row.AssignedAtUtc = source.AssignedAtUtc;
        row.Version = source.Version;
    }
}
