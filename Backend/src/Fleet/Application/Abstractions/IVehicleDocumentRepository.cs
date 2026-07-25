using NorthernLink.Fleet.Domain.Documents;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>Write-side persistence for the VehicleDocument aggregate (tenant-scoped).</summary>
public interface IVehicleDocumentRepository
{
    Task<VehicleDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    void Add(VehicleDocument document);

    void Remove(VehicleDocument document);

    /// <summary>Next per-tenant sequence for DOC-{seq} numbering.</summary>
    Task<int> NextSequenceAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
