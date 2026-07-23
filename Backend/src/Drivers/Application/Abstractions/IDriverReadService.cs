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
}
