using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// Write-side repository over <see cref="FleetDbContext"/>. Its caller is the
/// trip-manifest event consumer, which runs outside any HTTP request: the ambient
/// tenant filter would compare against null and match nothing, so the existence check
/// bypasses it and filters on the tenant id carried by the event instead.
/// </summary>
internal sealed class VehicleInspectionRepository(FleetDbContext context) : IVehicleInspectionRepository
{
    public Task<bool> ExistsForManifestAsync(
        Guid tenantId,
        Guid manifestId,
        InspectionType type,
        CancellationToken cancellationToken = default) =>
        context.VehicleInspections
            .IgnoreQueryFilters()
            .AnyAsync(
                i => i.TenantId == tenantId && i.ManifestId == manifestId && i.Type == type,
                cancellationToken);

    public Task<VehicleInspection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.VehicleInspections.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public void Add(VehicleInspection inspection) => context.VehicleInspections.Add(inspection);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
