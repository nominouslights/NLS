using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Fleet.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="VehicleInspection"/> into <c>fleet.rm_vehicle_inspections</c>. The
/// checklist and defect collections are copied into fresh lists (they persist as owned jsonb on
/// both sides), so the read row never aliases the aggregate's state.
/// </summary>
internal sealed class VehicleInspectionProjection : FleetProjection<VehicleInspection, VehicleInspectionReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(VehicleInspection));

    protected override void Map(VehicleInspection source, VehicleInspectionReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.Unit = source.Unit;
        row.Type = source.Type.ToString();
        row.DriverName = source.DriverName;
        row.Source = source.Source.ToString();
        row.EnteredBy = source.EnteredBy;
        row.TripNumber = source.TripNumber;
        row.ManifestId = source.ManifestId;
        row.GeneratedWorkOrderId = source.GeneratedWorkOrderId;
        row.PerformedAt = source.PerformedAt;
        row.OdometerKm = source.OdometerKm;
        row.Result = source.Result.ToString();
        row.ChecklistItems = [.. source.ChecklistItems];
        row.Defects = [.. source.Defects];
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.Version = source.Version;
    }
}
