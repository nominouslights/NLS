using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Trips.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="TripManifest"/> into <c>trips.rm_trip_manifests</c>.
///
/// Every query uses <c>IgnoreQueryFilters()</c>: the projection worker runs with no ambient
/// tenant, so the tenant query filter would compare against a null TenantId and match nothing.
/// Spanning tenants is intentional and is what the worker's pinned <c>app.is_system</c> session
/// authorises. Nothing is saved here — the worker commits with the checkpoint in one save.
///
/// Enum-typed scalars are written as their string forms (the response contract is strings anyway).
/// </summary>
internal sealed class TripManifestProjection : IProjection<TripsDbContext>
{
    public string AggregateType { get; } = AuditNames.ForAggregate(typeof(TripManifest));

    public async Task ApplyAsync(TripsDbContext context, Guid aggregateId, CancellationToken cancellationToken)
    {
        var manifest = await context.Manifests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == aggregateId, cancellationToken);

        var row = await context.ManifestReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == aggregateId, cancellationToken);

        if (manifest is null)
        {
            if (row is not null)
            {
                context.ManifestReadModels.Remove(row);
            }

            return;
        }

        if (row is null)
        {
            row = new TripManifestReadModel();
            Map(manifest, row);
            context.ManifestReadModels.Add(row);
        }
        else
        {
            Map(manifest, row);
        }
    }

    public async Task RebuildAllAsync(TripsDbContext context, CancellationToken cancellationToken)
    {
        var manifests = await context.Manifests.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var rows = await context.ManifestReadModels.IgnoreQueryFilters().ToListAsync(cancellationToken);

        var rowsById = rows.ToDictionary(row => row.Id);
        var seen = new HashSet<Guid>();

        foreach (var manifest in manifests)
        {
            seen.Add(manifest.Id);

            if (rowsById.TryGetValue(manifest.Id, out var existing))
            {
                Map(manifest, existing);
            }
            else
            {
                var fresh = new TripManifestReadModel();
                Map(manifest, fresh);
                context.ManifestReadModels.Add(fresh);
            }
        }

        foreach (var (id, row) in rowsById)
        {
            if (!seen.Contains(id))
            {
                context.ManifestReadModels.Remove(row);
            }
        }
    }

    private static void Map(TripManifest source, TripManifestReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;

        // §1.
        row.TripDate = source.TripDate;
        row.TripNumber = source.TripNumber;
        row.Route = source.Route;
        row.Direction = source.Direction?.ToString();
        row.Client = source.Client;

        // §5.
        row.Passengers = [.. source.Passengers];
        row.AllSeatbeltsVerified = source.AllSeatbeltsVerified;

        // §6.
        row.Cargo = [.. source.Cargo];
        row.AllCargoSecured = source.AllCargoSecured?.ToString();

        // Provenance.
        row.Source = source.Source.ToString();
        row.EnteredBy = source.EnteredBy;
        row.EnteredAt = source.EnteredAt;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.Version = source.Version;
    }
}
