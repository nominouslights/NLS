using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Vehicles;

namespace NorthernLink.Fleet.Tests;

/// <summary>In-memory fake of the write-side repository for handler tests.</summary>
internal sealed class InMemoryVehicleRepository : IVehicleRepository
{
    public List<Vehicle> Vehicles { get; } = [];

    public List<RetirementCertificate> Certificates { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Vehicles.FirstOrDefault(v => v.Id == id));

    public Task<Vehicle?> GetByUnitNumberAsync(string unitNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(Vehicles.FirstOrDefault(v => v.UnitNumber == unitNumber));

    public Task<bool> ExistsByVinAsync(
        Vin vin,
        Guid? excludeVehicleId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Vehicles.Any(v =>
            v.Vin.Value == vin.Value && (excludeVehicleId is null || v.Id != excludeVehicleId)));

    public Task<bool> ExistsByUnitNumberAsync(
        string unitNumber,
        Guid? excludeVehicleId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Vehicles.Any(v =>
            v.UnitNumber == unitNumber && (excludeVehicleId is null || v.Id != excludeVehicleId)));

    public void Add(Vehicle vehicle) => Vehicles.Add(vehicle);

    public void AddCertificate(RetirementCertificate certificate) => Certificates.Add(certificate);

    public Task<bool> HasCertificateAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Certificates.Any(c => c.VehicleId == vehicleId));

    public Task<int> NextCertificateSequenceAsync(
        Guid tenantId,
        int year,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Certificates.Count(c => c.TenantId == tenantId && c.IssuedAtUtc.Year == year) + 1);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
