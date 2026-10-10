using NorthernLink.Trips.Application.Integration;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Persistence for the <see cref="DriverLookup"/> replica. Reads are tenant-scoped
/// (EF query filter + RLS); the upsert runs from an integration handler under the
/// event's tenant (pushed as the ambient tenant) and is idempotent keyed on DriverId.
/// </summary>
public interface IDriverLookupRepository
{
    Task<DriverLookup?> GetAsync(Guid driverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The driver row the Identity user <paramref name="userId"/> is linked to, in the current
    /// tenant, or null when the account owns no driver. At most one per tenant (partial unique
    /// index on <c>(tenant_id, user_id)</c>).
    /// </summary>
    Task<DriverLookup?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task UpsertAsync(DriverLookup driver, CancellationToken cancellationToken = default);
}
