using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Vehicles;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// Read side — queries the materialized-view-backed read models through their tenant wrapper
/// views (<c>v_vehicles</c>, <c>v_retirement_certificates</c>) and maps to the public
/// contracts. Depreciation fields are now plain columns computed in SQL, so there is no
/// C# computed-property dependency on the read path.
/// </summary>
internal sealed class VehicleReadService(FleetDbContext context) : IVehicleReadService
{
    public async Task<IReadOnlyList<VehicleResponse>> GetVehiclesAsync(CancellationToken cancellationToken = default)
    {
        var vehicles = await context.VehicleReadModels
            .AsNoTracking()
            .OrderBy(v => v.UnitNumber)
            .ToListAsync(cancellationToken);

        return vehicles.Select(ToResponse).ToList();
    }

    public async Task<VehicleResponse?> GetVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await context.VehicleReadModels
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

        return vehicle is null ? null : ToResponse(vehicle);
    }

    public async Task<RetirementCertificateResponse?> GetRetirementCertificateAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default)
    {
        var certificate = await context.RetirementCertificateReadModels
            .AsNoTracking()
            .Where(c => c.VehicleId == vehicleId)
            .OrderByDescending(c => c.IssuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return certificate is null ? null : new RetirementCertificateResponse(
            certificate.Id,
            certificate.VehicleId,
            certificate.CertificateNumber,
            certificate.Vin,
            certificate.UnitNumber,
            certificate.Make,
            certificate.Model,
            certificate.Year,
            certificate.FinalOdometerKm,
            certificate.RetirementReason,
            certificate.RetiredAtUtc,
            certificate.IssuedAtUtc);
    }

    private static VehicleResponse ToResponse(ReadModels.VehicleReadModel v) => new(
        v.Id,
        v.UnitNumber,
        v.Vin,
        v.Make,
        v.Model,
        v.Year,
        v.SeatingCapacity,
        v.LicencePlate,
        v.RequiredLicenceClass,
        v.Status,
        v.StatusReason,
        v.OdometerKm,
        v.AcquisitionCostCad,
        v.EndOfLifeKm,
        v.CurrentValueCad,
        v.RemainingKm,
        v.LifeUsedPct,
        v.RequiresPeriodicInspection,
        v.SalePriceCad,
        v.DisposedAtUtc,
        v.CreatedAtUtc,
        v.UpdatedAtUtc);
}
