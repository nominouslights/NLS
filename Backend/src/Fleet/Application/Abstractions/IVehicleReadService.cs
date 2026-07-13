using NorthernLink.Fleet.Application.Vehicles;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>
/// Read side for vehicle queries — returns response DTOs directly, skipping the aggregate.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface IVehicleReadService
{
    Task<IReadOnlyList<VehicleResponse>> GetVehiclesAsync(CancellationToken cancellationToken = default);

    Task<VehicleResponse?> GetVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task<RetirementCertificateResponse?> GetRetirementCertificateAsync(Guid vehicleId, CancellationToken cancellationToken = default);
}
