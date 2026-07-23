using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Drivers;
using NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Read side — queries drivers.rm_drivers and maps to the public contract.</summary>
internal sealed class DriverReadService(DriversDbContext context) : IDriverReadService
{
    public async Task<IReadOnlyList<DriverResponse>> GetDriversAsync(CancellationToken cancellationToken = default)
    {
        var drivers = await context.DriverReadModels
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);

        return drivers.Select(ToResponse).ToList();
    }

    public async Task<DriverResponse?> GetDriverAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        var driver = await context.DriverReadModels
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);

        return driver is null ? null : ToResponse(driver);
    }

    private static DriverResponse ToResponse(DriverReadModel d) => new(
        d.Id,
        d.Name,
        d.Phone,
        d.LicenceClass,
        d.LicenceExpiry,
        d.Source,
        d.HasWorkPermit,
        d.Status,
        d.CredentialCount,
        d.SoonestCredentialExpiry,
        d.LatestDutyStatus,
        d.LatestDrivingHours,
        d.LatestHosDate,
        d.RegisteredAtUtc,
        d.UpdatedAtUtc);
}
