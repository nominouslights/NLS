using NorthernLink.Drivers.Domain.Credentials;

namespace NorthernLink.Drivers.Application.Abstractions;

/// <summary>Write-side persistence for the DriverCredential aggregate (tenant-scoped).</summary>
public interface IDriverCredentialRepository
{
    Task<DriverCredential?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when the driver exists for the current tenant — the referential guard.</summary>
    Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default);

    void Add(DriverCredential credential);

    void Remove(DriverCredential credential);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
