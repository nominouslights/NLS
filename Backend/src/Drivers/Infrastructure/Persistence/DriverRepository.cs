using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Drivers;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="DriversDbContext"/> (tenant-filtered).</summary>
internal sealed class DriverRepository(DriversDbContext context) : IDriverRepository
{
    public Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Drivers.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public void Add(Driver driver) => context.Drivers.Add(driver);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
