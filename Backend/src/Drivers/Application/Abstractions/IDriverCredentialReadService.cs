using NorthernLink.Drivers.Application.Credentials;

namespace NorthernLink.Drivers.Application.Abstractions;

/// <summary>Read side for driver credential queries (tenant-scoped).</summary>
public interface IDriverCredentialReadService
{
    Task<IReadOnlyList<DriverCredentialResponse>> GetForDriverAsync(
        Guid driverId, CancellationToken cancellationToken = default);
}
