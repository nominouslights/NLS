using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// Write-side repository over <see cref="FleetDbContext"/>. Its caller is the
/// trip-manifest event consumer, which runs outside any HTTP request: the ambient
/// tenant filter would compare against null and match nothing, so the existence check
/// bypasses it and filters on the tenant id carried by the event instead.
/// </summary>
internal sealed class VehicleInspectionRepository(FleetDbContext context) : IVehicleInspectionRepository
{
    public Task<bool> ExistsForManifestAsync(
        Guid tenantId,
        Guid manifestId,
        InspectionType type,
        CancellationToken cancellationToken = default) =>
        context.VehicleInspections
            .IgnoreQueryFilters()
            .AnyAsync(
                i => i.TenantId == tenantId && i.ManifestId == manifestId && i.Type == type,
                cancellationToken);

    public Task<bool> ExistsForTripAsync(
        Guid tenantId,
        string tripNumber,
        InspectionType type,
        CancellationToken cancellationToken = default) =>
        context.VehicleInspections
            .IgnoreQueryFilters()
            .AnyAsync(
                i => i.TenantId == tenantId && i.TripNumber == tripNumber && i.Type == type,
                cancellationToken);

    public Task<VehicleInspection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.VehicleInspections.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<VehicleInspection?> GetByGeneratedWorkOrderIdAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default) =>
        context.VehicleInspections
            .FirstOrDefaultAsync(i => i.GeneratedWorkOrderId == workOrderId, cancellationToken);

    /// <summary>
    /// Two predicates in SQL, both exact mirrors of a domain rule: the vehicle scope (the same
    /// shape as <c>VehicleDefectReadService</c>'s query, and <see cref="DefectVehicleScope.Includes"/>)
    /// and "has an unresolved defect" — Npgsql translates the <c>Defects.Any(...)</c> over the
    /// <c>ToJson</c> collection into an EXISTS over <c>jsonb_to_recordset("defects")</c>. There is
    /// NO item predicate: item matching is <c>VehicleInspection.IsSameItem</c>, applied in memory,
    /// so SQL can never exclude a row the domain would match. The tenant comes from the context's
    /// query filter (plus RLS), never from a manual predicate.
    /// </summary>
    public async Task<IReadOnlyList<VehicleInspection>> GetWithOpenDefectsAsync(
        DefectVehicleScope scope,
        CancellationToken cancellationToken = default)
    {
        var query = context.VehicleInspections.AsQueryable();
        var unit = scope.Unit;

        if (scope.VehicleId is { } vehicleId)
        {
            query = unit is null
                ? query.Where(i => i.VehicleId == vehicleId)
                : query.Where(i => i.VehicleId == vehicleId || (i.VehicleId == null && i.Unit == unit));
        }
        else
        {
            query = query.Where(i => i.VehicleId == null && i.Unit == unit);
        }

        return await query
            .Where(i => i.Defects.Any(d => d.ResolvedAtUtc == null))
            .ToListAsync(cancellationToken);
    }

    public void Add(VehicleInspection inspection) => context.VehicleInspections.Add(inspection);

    public void Remove(VehicleInspection inspection) => context.VehicleInspections.Remove(inspection);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // The transaction rolled back. The audit pipeline (ModuleDbContext.AppendAuditEntries)
            // had already bumped the aggregates' in-memory Version and staged snapshot/journal/
            // outbox rows for this write; none of those are in ex.Entries. Left tracked, a retry
            // would journal them twice — so drop everything (Budgeting's VendorRepository and
            // Compliance's TrySaveChangesAsync do the same).
            context.ChangeTracker.Clear();
            return false;
        }
    }

    public void DiscardChanges() => context.ChangeTracker.Clear();
}
