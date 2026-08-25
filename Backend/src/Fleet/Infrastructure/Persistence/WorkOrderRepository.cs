using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="FleetDbContext"/> (tenant-filtered).</summary>
internal sealed class WorkOrderRepository(FleetDbContext context) : IWorkOrderRepository
{
    public Task<WorkOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.WorkOrders.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.WorkOrders.AnyAsync(w => w.Id == id, cancellationToken);

    public Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        context.Vehicles.AnyAsync(v => v.Id == vehicleId, cancellationToken);

    public void Add(WorkOrder workOrder) => context.WorkOrders.Add(workOrder);

    public async Task<int> NextSequenceAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var existing = await context.WorkOrders.CountAsync(w => w.TenantId == tenantId, cancellationToken);
        return existing + 1;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
