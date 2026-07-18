using NorthernLink.Fleet.Application.Services;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>Read side for service-record queries (tenant-scoped).</summary>
public interface IServiceRecordReadService
{
    Task<IReadOnlyList<ServiceRecordResponse>> GetForVehicleAsync(
        Guid vehicleId, CancellationToken cancellationToken = default);
}
