using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>
/// Write-side persistence for the VehicleInspection aggregate. Unlike the request-path
/// repositories, its callers are integration event handlers running outside any HTTP
/// request (empty ITenantContext), so the existence check takes the tenant explicitly
/// from the event instead of relying on the ambient query filter.
/// </summary>
public interface IVehicleInspectionRepository
{
    /// <summary>True when this manifest already produced an inspection of this type — the consumer's idempotency check.</summary>
    Task<bool> ExistsForManifestAsync(
        Guid tenantId,
        Guid manifestId,
        InspectionType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True when this trip already has an inspection of this half — the one-pre-trip-and-one-
    /// post-trip-per-trip guard on entry. Takes the tenant explicitly (like
    /// <see cref="ExistsForManifestAsync"/>) so it is correct on both the request path and any
    /// tenant-less caller.
    /// </summary>
    Task<bool> ExistsForTripAsync(
        Guid tenantId,
        string tripNumber,
        InspectionType type,
        CancellationToken cancellationToken = default);

    /// <summary>Loads an inspection by id (tenant-filtered) — used to link a generated work order, amend, or remove.</summary>
    Task<VehicleInspection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(VehicleInspection inspection);

    /// <summary>Hard-deletes an inspection. The aggregate must raise its removal event first.</summary>
    void Remove(VehicleInspection inspection);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
