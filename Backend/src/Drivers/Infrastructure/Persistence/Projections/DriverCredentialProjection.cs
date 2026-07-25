using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Domain.Credentials;
using NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;

namespace NorthernLink.Drivers.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="DriverCredential"/> into <c>drivers.rm_driver_credentials</c> —
/// and, because rm_drivers denormalizes <c>credential_count</c> /
/// <c>soonest_credential_expiry</c>, refreshes the parent driver's read row in the same
/// batch. The parent driver id survives a hard delete via the stale read row, which is
/// looked up before it is removed.
/// </summary>
internal sealed class DriverCredentialProjection : IProjection<DriversDbContext>
{
    public string AggregateType { get; } = AuditNames.ForAggregate(typeof(DriverCredential));

    public async Task ApplyAsync(DriversDbContext context, Guid credentialId, CancellationToken cancellationToken)
    {
        var source = await context.DriverCredentials
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == credentialId, cancellationToken);

        var row = await context.DriverCredentialReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == credentialId, cancellationToken);

        // On delete the source is gone, so the read row is the only place the parent
        // driver id is still known — capture it before removing.
        var driverId = source?.DriverId ?? row?.DriverId;

        if (source is null)
        {
            if (row is not null)
            {
                context.DriverCredentialReadModels.Remove(row);
            }
        }
        else
        {
            if (row is null)
            {
                row = new DriverCredentialReadModel();
                context.DriverCredentialReadModels.Add(row);
            }

            Map(source, row);
        }

        if (driverId is { } id)
        {
            await RefreshDriverStatsAsync(context, id, cancellationToken);
        }
    }

    public async Task RebuildAllAsync(DriversDbContext context, CancellationToken cancellationToken)
    {
        var sources = await context.DriverCredentials.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var rows = await context.DriverCredentialReadModels.IgnoreQueryFilters().ToListAsync(cancellationToken);

        var rowsById = rows.ToDictionary(row => row.Id);
        var seen = new HashSet<Guid>();

        foreach (var source in sources)
        {
            seen.Add(source.Id);

            if (!rowsById.TryGetValue(source.Id, out var row))
            {
                row = new DriverCredentialReadModel();
                context.DriverCredentialReadModels.Add(row);
            }

            Map(source, row);
        }

        foreach (var (id, row) in rowsById)
        {
            if (!seen.Contains(id))
            {
                context.DriverCredentialReadModels.Remove(row);
            }
        }

        // rm_drivers stats are rebuilt by DriverProjection.RebuildAllAsync from the same
        // write tables, so no extra pass is needed here.
    }

    /// <summary>Recomputes the parent driver's denormalized credential fields from the write side.</summary>
    private static async Task RefreshDriverStatsAsync(
        DriversDbContext context,
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var driverRow = await context.DriverReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == driverId, cancellationToken);

        // No driver read row yet — DriverProjection will compute the stats when it
        // materializes the row (its ApplyAsync always recounts credentials).
        if (driverRow is null)
        {
            return;
        }

        var credentials = await context.DriverCredentials
            .IgnoreQueryFilters()
            .Where(c => c.DriverId == driverId)
            .ToListAsync(cancellationToken);

        DriverCredentialStats.Apply(driverRow, credentials);
    }

    private static void Map(DriverCredential source, DriverCredentialReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.DriverId = source.DriverId;
        row.Type = source.Type;
        row.Label = source.Label;
        row.Issued = source.Issued;
        row.Expiry = source.Expiry;
        row.Optional = source.Optional;
        row.Note = source.Note;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.Version = source.Version;
        row.ImageKey = source.ImageKey;
        row.ImageContentType = source.ImageContentType;
    }
}
