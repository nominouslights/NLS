using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Credentials;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="DriversDbContext"/> (tenant-filtered).</summary>
internal sealed class DriverCredentialRepository(DriversDbContext context) : IDriverCredentialRepository
{
    public Task<DriverCredential?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.DriverCredentials.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        context.Drivers.AnyAsync(d => d.Id == driverId, cancellationToken);

    public void Add(DriverCredential credential) => context.DriverCredentials.Add(credential);

    public void Remove(DriverCredential credential) => context.DriverCredentials.Remove(credential);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
