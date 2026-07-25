using NorthernLink.Drivers.Application.Hos;

namespace NorthernLink.Drivers.Application.Abstractions;

/// <summary>Read side for HOS log queries (tenant-scoped).</summary>
public interface IHosLogReadService
{
    Task<IReadOnlyList<HosEntryResponse>> GetForDriverAsync(
        Guid driverId, CancellationToken cancellationToken = default);
}
