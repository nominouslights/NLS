using NorthernLink.Drivers.Domain.Clearances;

namespace NorthernLink.Drivers.Application.Abstractions;

/// <summary>Write-side persistence for the DriverClearance aggregate (tenant-scoped).</summary>
public interface IDriverClearanceRepository
{
    Task<DriverClearance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when the driver exists for the current tenant — the referential guard.</summary>
    Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default);

    void Add(DriverClearance clearance);

    void Remove(DriverClearance clearance);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
