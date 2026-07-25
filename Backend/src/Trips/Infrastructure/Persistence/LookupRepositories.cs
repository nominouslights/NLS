using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Replica upserts over <see cref="TripsDbContext"/>. Reads go through the tenant query
/// filter (request path). The upserts run from integration handlers, whose DbContext was
/// constructed before the handler pushed the event's tenant as the ambient tenant — so
/// the context's captured TenantId is null and the query filter would match nothing;
/// the upsert therefore bypasses the filter and matches on (key, tenant) explicitly.
/// Postgres RLS still scopes the statement to the event's tenant: the session variable
/// is read at connection open, inside the ambient push.
/// </summary>
internal sealed class DriverLookupRepository(TripsDbContext context) : IDriverLookupRepository
{
    public Task<DriverLookup?> GetAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        context.DriverLookups.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DriverId == driverId, cancellationToken);

    public async Task UpsertAsync(DriverLookup driver, CancellationToken cancellationToken = default)
    {
        var existing = await context.DriverLookups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                d => d.DriverId == driver.DriverId && d.TenantId == driver.TenantId,
                cancellationToken);

        if (existing is null)
        {
            context.DriverLookups.Add(driver);
        }
        else
        {
            existing.Name = driver.Name;
            existing.LicenceClass = driver.LicenceClass;
            existing.Status = driver.Status;
            existing.UpdatedAtUtc = driver.UpdatedAtUtc;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>See <see cref="DriverLookupRepository"/> for the tenancy notes.</summary>
internal sealed class ClientLookupRepository(TripsDbContext context) : IClientLookupRepository
{
    public Task<ClientLookup?> GetAsync(Guid clientId, CancellationToken cancellationToken = default) =>
        context.ClientLookups.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);

    public async Task UpsertAsync(ClientLookup client, CancellationToken cancellationToken = default)
    {
        var existing = await context.ClientLookups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                c => c.ClientId == client.ClientId && c.TenantId == client.TenantId,
                cancellationToken);

        if (existing is null)
        {
            context.ClientLookups.Add(client);
        }
        else
        {
            existing.Name = client.Name;
            existing.Type = client.Type;
            existing.ServiceType = client.ServiceType;
            existing.Tag = client.Tag;
            existing.UpdatedAtUtc = client.UpdatedAtUtc;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
