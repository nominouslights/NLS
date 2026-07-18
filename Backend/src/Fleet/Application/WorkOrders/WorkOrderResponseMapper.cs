using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Application.WorkOrders;

/// <summary>Maps the WorkOrder aggregate to its public response contract.</summary>
public static class WorkOrderResponseMapper
{
    public static WorkOrderResponse ToResponse(WorkOrder workOrder) => new(
        workOrder.Id,
        workOrder.VehicleId,
        workOrder.Number,
        workOrder.Title,
        workOrder.Description,
        workOrder.Status.ToString(),
        workOrder.Priority.ToString(),
        workOrder.Source.ToString(),
        workOrder.SourceRef,
        workOrder.CreatedBy,
        workOrder.CreatedAt,
        workOrder.AssignedTo,
        workOrder.DueDate,
        workOrder.LineItems,
        workOrder.CompletedAt,
        workOrder.ResolvingServiceId,
        workOrder.ShopId,
        workOrder.AuthorizedLimitCad,
        workOrder.BudgetCode,
        workOrder.DateRequiredOrOos);
}
