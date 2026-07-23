using NorthernLink.Trips.Application.Integration;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Persistence for the <see cref="ClientLookup"/> replica. Reads are tenant-scoped
/// (EF query filter + RLS); the upsert runs from an integration handler under the
/// event's tenant (pushed as the ambient tenant) and is idempotent keyed on ClientId.
/// </summary>
public interface IClientLookupRepository
{
    Task<ClientLookup?> GetAsync(Guid clientId, CancellationToken cancellationToken = default);

    Task UpsertAsync(ClientLookup client, CancellationToken cancellationToken = default);
}
