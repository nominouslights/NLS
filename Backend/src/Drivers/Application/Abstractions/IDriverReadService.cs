using NorthernLink.Drivers.Application.Drivers;

namespace NorthernLink.Drivers.Application.Abstractions;

/// <summary>
/// Read side for driver queries — returns response DTOs from the rm_drivers projection,
/// skipping the aggregate. Implementations are tenant-scoped (EF global query filter +
/// Postgres RLS).
/// </summary>
public interface IDriverReadService
{
    Task<IReadOnlyList<DriverResponse>> GetDriversAsync(CancellationToken cancellationToken = default);

    Task<DriverResponse?> GetDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The roster row linked to an Identity user, or null when that account has none — what
    /// <c>GET /api/drivers/me</c> serves. Read side, so it carries the same denormalized
    /// credential and HOS rollups the roster list does, and the same projection lag.
    /// </summary>
    Task<DriverResponse?> GetDriverByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
