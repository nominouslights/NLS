using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="FleetDbContext"/> (tenant-filtered).</summary>
internal sealed class MaintenancePlanRepository(FleetDbContext context) : IMaintenancePlanRepository
{
    public Task<MaintenancePlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.MaintenancePlans.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.MaintenancePlans.AnyAsync(p => p.Id == id, cancellationToken);

    public Task<bool> ExistsByNameAsync(
        string name, Guid? excludePlanId = null, CancellationToken cancellationToken = default) =>
        // Dumb equality — trim ownership sits with the caller, matching what the aggregate stores.
        context.MaintenancePlans.AnyAsync(
            p => p.Name == name && (excludePlanId == null || p.Id != excludePlanId),
            cancellationToken);

    public void Add(MaintenancePlan plan) => context.MaintenancePlans.Add(plan);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
