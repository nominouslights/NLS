using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="FleetDbContext"/> (tenant-filtered).</summary>
internal sealed class MaintenancePlanRepository(FleetDbContext context) : IMaintenancePlanRepository
{
    public Task<MaintenancePlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.MaintenancePlans.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<MaintenancePlan?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        // Stored names are trimmed by the aggregate, so trim the probe too — an untrimmed
        // input must still find its match.
        var trimmed = name.Trim();
        return context.MaintenancePlans.FirstOrDefaultAsync(p => p.Name == trimmed, cancellationToken);
    }

    public void Add(MaintenancePlan plan) => context.MaintenancePlans.Add(plan);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
