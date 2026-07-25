using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Services;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="FleetDbContext"/> (tenant-filtered).</summary>
internal sealed class ServiceRecordRepository(FleetDbContext context) : IServiceRecordRepository
{
    public Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        context.Vehicles.AnyAsync(v => v.Id == vehicleId, cancellationToken);

    public void Add(ServiceRecord record) => context.ServiceRecords.Add(record);

    public async Task<int> NextSequenceAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var existing = await context.ServiceRecords.CountAsync(s => s.TenantId == tenantId, cancellationToken);
        return existing + 1;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
