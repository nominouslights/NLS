using NorthernLink.Fleet.Application.Documents;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>Read side for vehicle document queries (tenant-scoped).</summary>
public interface IVehicleDocumentReadService
{
    Task<IReadOnlyList<VehicleDocumentResponse>> GetForVehicleAsync(
        Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Fleet-wide — powers the compliance-watch panel on the dashboard.</summary>
    Task<IReadOnlyList<VehicleDocumentResponse>> GetAllAsync(CancellationToken cancellationToken = default);
}
