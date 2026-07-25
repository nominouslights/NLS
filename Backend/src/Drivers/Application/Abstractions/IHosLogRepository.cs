using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Application.Abstractions;

/// <summary>Write-side persistence for the HosLogEntry aggregate (tenant-scoped).</summary>
public interface IHosLogRepository
{
    Task<HosLogEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when the driver exists for the current tenant — the referential guard.</summary>
    Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default);

    void Add(HosLogEntry entry);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
