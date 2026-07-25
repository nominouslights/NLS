using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.WorkOrders;
using NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Read side — queries the read model through fleet.v_work_orders and maps to the public contract.</summary>
internal sealed class WorkOrderReadService(FleetDbContext context) : IWorkOrderReadService
{
    public async Task<IReadOnlyList<WorkOrderResponse>> GetForVehicleAsync(
        Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var workOrders = await context.WorkOrderReadModels
            .AsNoTracking()
            .Where(w => w.VehicleId == vehicleId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        return workOrders.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<WorkOrderResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var workOrders = await context.WorkOrderReadModels
            .AsNoTracking()
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        return workOrders.Select(ToResponse).ToList();
    }

    private static WorkOrderResponse ToResponse(WorkOrderReadModel w) => new(
        w.Id,
        w.VehicleId,
        w.Number,
        w.Title,
        w.Description,
        w.Status,
        w.Priority,
        w.Source,
        w.SourceRef,
        w.CreatedBy,
        w.CreatedAt,
        w.AssignedTo,
        w.DueDate,
        w.LineItems,
        w.CompletedAt,
        w.ResolvingServiceId,
        w.ShopId,
        w.AuthorizedLimitCad,
        w.BudgetCode,
        w.DateRequiredOrOos);
}
