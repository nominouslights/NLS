using NorthernLink.Drivers.Application.Clearances;

namespace NorthernLink.Drivers.Application.Abstractions;

/// <summary>Read side for driver clearance queries (tenant-scoped).</summary>
public interface IDriverClearanceReadService
{
    Task<IReadOnlyList<DriverClearanceResponse>> GetForDriverAsync(
        Guid driverId, CancellationToken cancellationToken = default);
}
