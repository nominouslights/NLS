using NorthernLink.Fleet.Application.Inspections;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>
/// Read side for inspection queries — returns response DTOs directly, skipping the
/// aggregate. Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface IVehicleInspectionReadService
{
    /// <summary>Lists inspections, optionally narrowed to a vehicle unit, newest first.</summary>
    Task<IReadOnlyList<VehicleInspectionResponse>> GetInspectionsAsync(
        string? unit = null,
        CancellationToken cancellationToken = default);
}
