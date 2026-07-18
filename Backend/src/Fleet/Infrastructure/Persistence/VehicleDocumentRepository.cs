using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Documents;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="FleetDbContext"/> (tenant-filtered).</summary>
internal sealed class VehicleDocumentRepository(FleetDbContext context) : IVehicleDocumentRepository
{
    public Task<VehicleDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.VehicleDocuments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        context.Vehicles.AnyAsync(v => v.Id == vehicleId, cancellationToken);

    public void Add(VehicleDocument document) => context.VehicleDocuments.Add(document);

    public void Remove(VehicleDocument document) => context.VehicleDocuments.Remove(document);

    public async Task<int> NextSequenceAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var existing = await context.VehicleDocuments.CountAsync(d => d.TenantId == tenantId, cancellationToken);
        return existing + 1;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
