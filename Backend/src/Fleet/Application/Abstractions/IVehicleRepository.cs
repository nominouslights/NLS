using NorthernLink.Fleet.Domain.Vehicles;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>
/// Write-side persistence for the Vehicle aggregate and its retirement certificates.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads a vehicle by its (tenant-unique) unit number — the fallback link when an inspection carries no vehicle id.</summary>
    Task<Vehicle?> GetByUnitNumberAsync(string unitNumber, CancellationToken cancellationToken = default);

    /// <summary>Loads a vehicle by its VIN — the primary match when the PM seed links its plan to unit NL-01.</summary>
    Task<Vehicle?> GetByVinAsync(Vin vin, CancellationToken cancellationToken = default);

    /// <summary>True when another vehicle (excluding <paramref name="excludeVehicleId"/>) already uses this VIN.</summary>
    Task<bool> ExistsByVinAsync(Vin vin, Guid? excludeVehicleId = null, CancellationToken cancellationToken = default);

    /// <summary>True when another vehicle (excluding <paramref name="excludeVehicleId"/>) already uses this unit number.</summary>
    Task<bool> ExistsByUnitNumberAsync(string unitNumber, Guid? excludeVehicleId = null, CancellationToken cancellationToken = default);

    void Add(Vehicle vehicle);

    void AddCertificate(RetirementCertificate certificate);

    /// <summary>True when a retirement certificate already exists for the vehicle (idempotency guard).</summary>
    Task<bool> HasCertificateAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Next per-tenant sequence for RC-{year}-{seq} certificate numbering.</summary>
    Task<int> NextCertificateSequenceAsync(Guid tenantId, int year, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
