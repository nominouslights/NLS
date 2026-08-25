using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>Write-side persistence for the WorkOrder aggregate (tenant-scoped).</summary>
public interface IWorkOrderRepository
{
    Task<WorkOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The vehicle a work order belongs to, or null when the work order does not exist —
    /// the link-validation probe: one query that distinguishes "no such work order" from
    /// "another vehicle's work order" without materializing the aggregate.
    /// </summary>
    Task<Guid?> FindVehicleIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    void Add(WorkOrder workOrder);

    /// <summary>Next per-tenant sequence for WO-{seq} numbering.</summary>
    Task<int> NextSequenceAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
