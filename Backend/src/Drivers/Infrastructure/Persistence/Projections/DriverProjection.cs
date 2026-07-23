using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Domain.Credentials;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;

namespace NorthernLink.Drivers.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="Driver"/> into <c>drivers.rm_drivers</c>. Can't use the generic
/// one-row base: the read row also carries the denormalized credential fields
/// (<c>credential_count</c>, <c>soonest_credential_expiry</c>), which are computed here
/// from the write-side credentials — and kept fresh on credential changes by
/// <see cref="DriverCredentialProjection"/>, which recomputes the same two fields.
/// </summary>
internal sealed class DriverProjection : IProjection<DriversDbContext>
{
    public string AggregateType { get; } = AuditNames.ForAggregate(typeof(Driver));

    public async Task ApplyAsync(DriversDbContext context, Guid driverId, CancellationToken cancellationToken)
    {
        var source = await context.Drivers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);

        var row = await context.DriverReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == driverId, cancellationToken);

        // Source gone (hard delete) — the read row goes with it.
        if (source is null)
        {
            if (row is not null)
            {
                context.DriverReadModels.Remove(row);
            }

            return;
        }

        if (row is null)
        {
            row = new DriverReadModel();
            context.DriverReadModels.Add(row);
        }

        Map(source, row);

        var credentials = await context.DriverCredentials
            .IgnoreQueryFilters()
            .Where(c => c.DriverId == driverId)
            .ToListAsync(cancellationToken);

        DriverCredentialStats.Apply(row, credentials);

        var hosEntries = await context.HosLogEntries
            .IgnoreQueryFilters()
            .Where(e => e.DriverId == driverId)
            .ToListAsync(cancellationToken);

        HosStats.Apply(row, hosEntries);
    }

    public async Task RebuildAllAsync(DriversDbContext context, CancellationToken cancellationToken)
    {
        var sources = await context.Drivers.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var rows = await context.DriverReadModels.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var credentialsByDriver = (await context.DriverCredentials.IgnoreQueryFilters().ToListAsync(cancellationToken))
            .GroupBy(c => c.DriverId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var hosByDriver = (await context.HosLogEntries.IgnoreQueryFilters().ToListAsync(cancellationToken))
            .GroupBy(e => e.DriverId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var rowsById = rows.ToDictionary(row => row.Id);
        var seen = new HashSet<Guid>();

        foreach (var source in sources)
        {
            seen.Add(source.Id);

            if (!rowsById.TryGetValue(source.Id, out var row))
            {
                row = new DriverReadModel();
                context.DriverReadModels.Add(row);
            }

            Map(source, row);
            DriverCredentialStats.Apply(
                row,
                credentialsByDriver.TryGetValue(source.Id, out var credentials) ? credentials : []);
            HosStats.Apply(
                row,
                hosByDriver.TryGetValue(source.Id, out var hosEntries) ? hosEntries : []);
        }

        // Orphans: read rows whose source aggregate no longer exists.
        foreach (var (id, row) in rowsById)
        {
            if (!seen.Contains(id))
            {
                context.DriverReadModels.Remove(row);
            }
        }
    }

    private static void Map(Driver source, DriverReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.Name = source.Name;
        row.Phone = source.Phone;
        row.LicenceClass = source.LicenceClass;
        row.LicenceExpiry = source.LicenceExpiry;
        row.Source = source.Source;
        row.HasWorkPermit = source.HasWorkPermit;
        row.Status = source.Status.ToString();
        row.RegisteredAtUtc = source.RegisteredAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }
}

/// <summary>
/// The single implementation of the rm_drivers denormalized credential fields, shared by
/// <see cref="DriverProjection"/> and <see cref="DriverCredentialProjection"/> so the two
/// writers can never disagree.
/// </summary>
internal static class DriverCredentialStats
{
    public static void Apply(DriverReadModel row, IReadOnlyCollection<DriverCredential> credentials)
    {
        row.CredentialCount = credentials.Count;
        row.SoonestCredentialExpiry = credentials
            .Where(c => c.Expiry is not null)
            .Select(c => c.Expiry)
            .Min();
    }
}
