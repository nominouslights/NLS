using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Clearances;
using NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Read side — queries drivers.rm_driver_clearances and maps to the public contract.</summary>
internal sealed class DriverClearanceReadService(DriversDbContext context) : IDriverClearanceReadService
{
    public async Task<IReadOnlyList<DriverClearanceResponse>> GetForDriverAsync(
        Guid driverId, CancellationToken cancellationToken = default)
    {
        var clearances = await context.DriverClearanceReadModels
            .AsNoTracking()
            .Where(c => c.DriverId == driverId)
            .OrderByDescending(c => c.GrantedAtUtc)
            .ToListAsync(cancellationToken);

        return clearances.Select(ToResponse).ToList();
    }

    private static DriverClearanceResponse ToResponse(DriverClearanceReadModel c) => new(
        c.Id,
        c.DriverId,
        c.Title,
        c.ClientName,
        c.Expiry,
        c.GrantedAtUtc);
}
