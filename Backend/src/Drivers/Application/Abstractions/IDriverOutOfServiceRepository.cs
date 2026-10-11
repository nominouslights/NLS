using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Application.Abstractions;

/// <summary>
/// Write-side persistence for <see cref="DriverOutOfService"/> (tenant-scoped). Shares the
/// module's unit of work with the other Drivers repositories, so one
/// <see cref="SaveChangesAsync"/> commits a declaration and the finding it opens together.
/// </summary>
public interface IDriverOutOfServiceRepository
{
    Task<DriverOutOfService?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The driver's records whose status is still <see cref="OutOfServiceStatus.Active"/> (expired-but-unlifted included; the handler checks the clock).</summary>
    Task<IReadOnlyList<DriverOutOfService>> ActiveForDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    void Add(DriverOutOfService record);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
