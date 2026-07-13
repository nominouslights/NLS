using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Vehicles;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// Read side — queries the entity sets directly (no aggregate behavior) and maps to the
/// public contracts in memory, because the depreciation fields are computed properties.
/// </summary>
internal sealed class VehicleReadService(FleetDbContext context) : IVehicleReadService
{
    public async Task<IReadOnlyList<VehicleResponse>> GetVehiclesAsync(CancellationToken cancellationToken = default)
    {
        var vehicles = await context.Vehicles
            .AsNoTracking()
            .OrderBy(v => v.UnitNumber)
            .ToListAsync(cancellationToken);

        return vehicles.Select(VehicleResponseMapper.ToResponse).ToList();
    }

    public async Task<VehicleResponse?> GetVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await context.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        return vehicle is null ? null : VehicleResponseMapper.ToResponse(vehicle);
    }

    public async Task<RetirementCertificateResponse?> GetRetirementCertificateAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default)
    {
        var certificate = await context.RetirementCertificates
            .AsNoTracking()
            .Where(c => c.VehicleId == vehicleId)
            .OrderByDescending(c => c.IssuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return certificate is null ? null : VehicleResponseMapper.ToResponse(certificate);
    }
}
