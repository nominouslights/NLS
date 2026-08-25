using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Fleet.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="MaintenancePlan"/> into <c>fleet.rm_maintenance_plans</c>. The item and
/// overhaul collections are copied into fresh list instances; the element records themselves
/// are shared with the aggregate, which is safe because they are immutable records.
/// </summary>
internal sealed class MaintenancePlanProjection : FleetProjection<MaintenancePlan, MaintenancePlanReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(MaintenancePlan));

    protected override void Map(MaintenancePlan source, MaintenancePlanReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.Name = source.Name;
        row.VehicleModel = source.VehicleModel;
        row.ServiceClass = source.ServiceClass;
        row.Notes = source.Notes;
        row.Items = [.. source.Items];
        row.Overhauls = [.. source.Overhauls];
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}
