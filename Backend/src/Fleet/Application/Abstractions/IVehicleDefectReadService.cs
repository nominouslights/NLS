using NorthernLink.Fleet.Application.Inspections;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>
/// Read side for a vehicle's defect backlog (tenant-scoped, like every read service here).
///
/// Deliberately its own interface rather than another member on
/// <see cref="IVehicleInspectionReadService"/>: defects are their own read concern — their own
/// endpoint, DTO, resolved/unresolved semantics, and recurrence derivation, sharing nothing with
/// the inspection list beyond the DbContext. Segregating it also means no existing implementor
/// (production or test double) has to change to accommodate a feature it knows nothing about.
/// </summary>
public interface IVehicleDefectReadService
{
    /// <summary>
    /// Every defect reported on this vehicle's DVIRs, open ones only unless
    /// <paramref name="includeResolved"/> is set. Pre-ordered — OutOfService, then Major, then
    /// Minor, newest first within each — because the four surfaces that render this must agree.
    /// An unknown vehicle id yields an empty list, not an error.
    /// </summary>
    Task<IReadOnlyList<VehicleDefectResponse>> GetDefectsForVehicleAsync(
        Guid vehicleId,
        bool includeResolved,
        CancellationToken cancellationToken = default);
}
