using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Credentials;
using NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Read side — queries drivers.rm_driver_credentials and maps to the public contract.</summary>
internal sealed class DriverCredentialReadService(DriversDbContext context) : IDriverCredentialReadService
{
    public async Task<IReadOnlyList<DriverCredentialResponse>> GetForDriverAsync(
        Guid driverId, CancellationToken cancellationToken = default)
    {
        var credentials = await context.DriverCredentialReadModels
            .AsNoTracking()
            .Where(c => c.DriverId == driverId)
            .OrderByDescending(c => c.Issued)
            .ToListAsync(cancellationToken);

        return credentials.Select(ToResponse).ToList();
    }

    private static DriverCredentialResponse ToResponse(DriverCredentialReadModel c) => new(
        c.Id,
        c.DriverId,
        c.Type,
        c.Label,
        c.Issued,
        c.Expiry,
        c.Optional,
        c.Note,
        c.ImageKey is not null,
        c.ImageKey,
        c.ImageContentType);
}
