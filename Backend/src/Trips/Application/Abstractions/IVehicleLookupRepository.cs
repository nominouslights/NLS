using NorthernLink.Trips.Application.Integration;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Persistence for the <see cref="VehicleLookup"/> replica. Reads are tenant-scoped
/// (EF query filter + RLS); the upsert runs from an integration handler under the
/// event's tenant (pushed as the ambient tenant) and is idempotent keyed on VehicleId.
/// Mirrors <see cref="IDriverLookupRepository"/>.
/// </summary>
public interface IVehicleLookupRepository
{
    Task<VehicleLookup?> GetAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The replica row for a unit number, exact match — how a schedule template's
    /// <c>DefaultVehicleUnit</c> resolves to a fleet vehicle at generation time.
    /// </summary>
    Task<VehicleLookup?> GetByUnitNumberAsync(string unitNumber, CancellationToken cancellationToken = default);

    Task UpsertAsync(VehicleLookup vehicle, CancellationToken cancellationToken = default);
}
