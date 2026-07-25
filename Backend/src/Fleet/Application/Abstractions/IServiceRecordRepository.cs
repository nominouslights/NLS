using NorthernLink.Fleet.Domain.Services;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>Write-side persistence for the ServiceRecord aggregate (tenant-scoped).</summary>
public interface IServiceRecordRepository
{
    Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    void Add(ServiceRecord record);

    /// <summary>Next per-tenant sequence for SVC-{seq} numbering.</summary>
    Task<int> NextSequenceAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
