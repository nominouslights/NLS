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
    /// Vehicle scope AND the item/open filter both run in SQL: Npgsql translates the
    /// <c>Defects.Any(...)</c> over the <c>ToJson</c> collection into an EXISTS over
    /// <c>jsonb_to_recordset("defects")</c>, so only inspections that actually carry an open
    /// defect for the component are materialized and tracked. The item comparison is
    /// <c>lower(btrim(item)) = lower(@key)</c>; Postgres <c>lower</c> and .NET
    /// <c>OrdinalIgnoreCase</c> can disagree on exotic Unicode, which is why the domain re-checks
    /// every row with the canonical rule. The tenant comes from the context's query filter (plus
    /// RLS), never from a manual predicate.
    /// </summary>
    public async Task<IReadOnlyList<VehicleInspection>> GetWithOpenDefectAsync(
        DefectVehicleScope scope,
        string item,
        CancellationToken cancellationToken = default)
    {
        var key = (item ?? string.Empty).Trim().ToLowerInvariant();
        var query = context.VehicleInspections.AsQueryable();

        if (scope.VehicleId is { } vehicleId)
        {
            var unit = scope.Unit;
            query = unit is null
                ? query.Where(i => i.VehicleId == vehicleId)
                : query.Where(i => i.VehicleId == vehicleId || (i.VehicleId == null && i.Unit == unit));
        }
        else
        {
            var unit = scope.Unit ?? string.Empty;
            query = query.Where(i => i.Unit == unit);
        }

        return await query
            .Where(i => i.Defects.Any(d => d.ResolvedAtUtc == null && d.Item.Trim().ToLower() == key))
            .ToListAsync(cancellationToken);
    }

    public void Add(VehicleInspection inspection) => context.VehicleInspections.Add(inspection);

    public void Remove(VehicleInspection inspection) => context.VehicleInspections.Remove(inspection);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
