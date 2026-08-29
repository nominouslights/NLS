using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Fleet.Infrastructure.Persistence.Projections;

/// <summary>Projects <see cref="PmCompletion"/> into <c>fleet.rm_pm_completions</c>.</summary>
internal sealed class PmCompletionProjection : FleetProjection<PmCompletion, PmCompletionReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(PmCompletion));

    protected override void Map(PmCompletion source, PmCompletionReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.VehicleId = source.VehicleId;
        row.PlanId = source.PlanId;
        row.ItemCode = source.ItemCode;
        row.Kind = source.Kind.ToString();
        row.PerformedAt = source.PerformedAt;
        row.OdometerKm = source.OdometerKm;
        row.PerformedBy = source.PerformedBy;
        row.WorkOrderId = source.WorkOrderId;
        row.Measurement = source.Measurement;
        row.Notes = source.Notes;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.Version = source.Version;
    }
}
