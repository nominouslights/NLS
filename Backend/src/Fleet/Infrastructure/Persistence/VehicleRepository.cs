using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Vehicles;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="FleetDbContext"/> (tenant-filtered).</summary>
internal sealed class VehicleRepository(FleetDbContext context) : IVehicleRepository
{
    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public Task<Vehicle?> GetByUnitNumberAsync(string unitNumber, CancellationToken cancellationToken = default) =>
        context.Vehicles.FirstOrDefaultAsync(v => v.UnitNumber == unitNumber, cancellationToken);

    public Task<Vehicle?> GetByVinAsync(Vin vin, CancellationToken cancellationToken = default) =>
        context.Vehicles.FirstOrDefaultAsync(v => v.Vin == vin, cancellationToken);

    public Task<bool> ExistsByVinAsync(
        Vin vin,
        Guid? excludeVehicleId = null,
        CancellationToken cancellationToken = default) =>
        context.Vehicles.AnyAsync(
            v => v.Vin == vin && (excludeVehicleId == null || v.Id != excludeVehicleId),
            cancellationToken);

    public Task<bool> ExistsByUnitNumberAsync(
        string unitNumber,
        Guid? excludeVehicleId = null,
        CancellationToken cancellationToken = default) =>
        context.Vehicles.AnyAsync(
            v => v.UnitNumber == unitNumber && (excludeVehicleId == null || v.Id != excludeVehicleId),
            cancellationToken);

    public void Add(Vehicle vehicle) => context.Vehicles.Add(vehicle);

    public void AddCertificate(RetirementCertificate certificate) =>
        context.RetirementCertificates.Add(certificate);

    public Task<bool> HasCertificateAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        context.RetirementCertificates.AnyAsync(c => c.VehicleId == vehicleId, cancellationToken);

    public async Task<int> NextCertificateSequenceAsync(
        Guid tenantId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var yearStart = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var yearEnd = yearStart.AddYears(1);

        // Count-based sequencing inside the unit of work; the unique index on
        // (tenant_id, certificate_number) guards against a concurrent duplicate.
        var issuedThisYear = await context.RetirementCertificates.CountAsync(
            c => c.TenantId == tenantId && c.IssuedAtUtc >= yearStart && c.IssuedAtUtc < yearEnd,
            cancellationToken);

        return issuedThisYear + 1;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
