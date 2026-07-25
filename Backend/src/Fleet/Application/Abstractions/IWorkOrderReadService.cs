using NorthernLink.Fleet.Application.WorkOrders;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>Read side for work-order queries (tenant-scoped).</summary>
public interface IWorkOrderReadService
{
    Task<IReadOnlyList<WorkOrderResponse>> GetForVehicleAsync(
        Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Fleet-wide — powers the dashboard work-order queue.</summary>
    Task<IReadOnlyList<WorkOrderResponse>> GetAllAsync(CancellationToken cancellationToken = default);
}
