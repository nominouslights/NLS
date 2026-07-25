using NorthernLink.Fleet.Domain.Services;
using NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Fleet.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="ServiceRecord"/> into <c>fleet.rm_service_records</c>. The collections are
/// copied into fresh lists rather than shared by reference, so the read row never aliases the
/// aggregate's state.
/// </summary>
internal sealed class ServiceRecordProjection : FleetProjection<ServiceRecord, ServiceRecordReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(ServiceRecord));

    protected override void Map(ServiceRecord source, ServiceRecordReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.VehicleId = source.VehicleId;
        row.Number = source.Number;
        row.Date = source.Date;
        row.PerformedBy = source.PerformedBy;
        row.Category = source.Category.ToString();
        row.OdometerKm = source.OdometerKm;
        row.ItemsChanged = [.. source.ItemsChanged];
        row.Reason = source.Reason;
        row.PartsUsed = [.. source.PartsUsed];
        row.LaborHours = source.LaborHours;
        row.CostCad = source.CostCad;
        row.WorkOrderId = source.WorkOrderId;
        row.Notes = source.Notes;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.Version = source.Version;
    }
}
