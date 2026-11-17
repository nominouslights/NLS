using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections;

/// <summary>
/// The loading half of clearing duplicate defects, shared by the manual resolve and work-order
/// completion so the two cannot disagree: work out which vehicle the source inspection belongs
/// to, load the candidate inspections (tracked, on the caller's scoped DbContext), and hand both
/// to the domain rule in <see cref="DuplicateDefects"/>. It saves nothing — the caller commits
/// everything in its one <c>SaveChangesAsync</c>.
/// </summary>
public static class DuplicateDefectCascade
{
    /// <summary>
    /// Clears the open duplicates of <paramref name="source"/>'s defect <paramref name="item"/>,
    /// which the caller has just resolved. Returns them oldest report first.
    /// </summary>
    public static async Task<IReadOnlyList<DefectOnInspection>> ResolveAsync(
        IVehicleInspectionRepository inspections,
        IVehicleRepository vehicles,
        VehicleInspection source,
        string item,
        CancellationToken cancellationToken)
    {
        var scope = await ScopeForAsync(vehicles, source, cancellationToken);
        var candidates = await inspections.GetWithOpenDefectAsync(scope, item, cancellationToken);
        return DuplicateDefects.Resolve(source, item, scope, candidates);
    }

    /// <summary>
    /// The defects panel's vehicle rule needs the linked vehicle's CURRENT unit number (to pick up
    /// legacy unit-only DVIRs), which the inspection itself does not carry — its own unit is
    /// display text as of entry. A missing vehicle leaves the scope id-only, the panel's fallback.
    /// </summary>
    private static async Task<DefectVehicleScope> ScopeForAsync(
        IVehicleRepository vehicles,
        VehicleInspection source,
        CancellationToken cancellationToken)
    {
        if (source.VehicleId is not { } vehicleId)
        {
            return DefectVehicleScope.Of(source, linkedVehicleUnitNumber: null);
        }

        var vehicle = await vehicles.GetByIdAsync(vehicleId, cancellationToken);
        return DefectVehicleScope.Of(source, vehicle?.UnitNumber);
    }
}
