using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="DriversDbContext"/> (tenant-filtered).</summary>
internal sealed class HosLogRepository(DriversDbContext context) : IHosLogRepository
{
    public Task<HosLogEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.HosLogEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        context.Drivers.AnyAsync(d => d.Id == driverId, cancellationToken);

    public void Add(HosLogEntry entry) => context.HosLogEntries.Add(entry);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
