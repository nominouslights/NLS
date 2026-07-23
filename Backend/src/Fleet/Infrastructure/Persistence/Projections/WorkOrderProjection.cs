using NorthernLink.Fleet.Domain.WorkOrders;
using NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Fleet.Infrastructure.Persistence.Projections;

/// <summary>Projects <see cref="WorkOrder"/> into <c>fleet.rm_work_orders</c>.</summary>
internal sealed class WorkOrderProjection : FleetProjection<WorkOrder, WorkOrderReadModel>
{
    public override string AggregateType { get; } = AuditNames.ForAggregate(typeof(WorkOrder));

    protected override void Map(WorkOrder source, WorkOrderReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.VehicleId = source.VehicleId;
        row.Number = source.Number;
        row.Title = source.Title;
        row.Description = source.Description;
        row.Status = source.Status.ToString();
        row.Priority = source.Priority.ToString();
        row.Source = source.Source.ToString();
        row.SourceRef = source.SourceRef;
        row.CreatedBy = source.CreatedBy;
        row.CreatedAt = source.CreatedAt;
        row.AssignedTo = source.AssignedTo;
        row.DueDate = source.DueDate;
        row.LineItems = [.. source.LineItems];
        row.CompletedAt = source.CompletedAt;
        row.ResolvingServiceId = source.ResolvingServiceId;
        row.ShopId = source.ShopId;
        row.AuthorizedLimitCad = source.AuthorizedLimitCad;
        row.BudgetCode = source.BudgetCode;
        row.DateRequiredOrOos = source.DateRequiredOrOos;
        row.Version = source.Version;
    }
}
