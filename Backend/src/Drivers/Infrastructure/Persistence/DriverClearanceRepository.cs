using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Clearances;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="DriversDbContext"/> (tenant-filtered).</summary>
internal sealed class DriverClearanceRepository(DriversDbContext context) : IDriverClearanceRepository
{
    public Task<DriverClearance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.DriverClearances.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        context.Drivers.AnyAsync(d => d.Id == driverId, cancellationToken);

    public void Add(DriverClearance clearance) => context.DriverClearances.Add(clearance);

    public void Remove(DriverClearance clearance) => context.DriverClearances.Remove(clearance);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
