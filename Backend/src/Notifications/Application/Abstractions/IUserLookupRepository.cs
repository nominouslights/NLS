using NorthernLink.Notifications.Application.Integration;

namespace NorthernLink.Notifications.Application.Abstractions;

/// <summary>
/// Persistence for the <see cref="UserLookup"/> replica. Both members run from integration
/// handlers under the event's tenant (pushed as the ambient tenant by the polling consumer
/// before the handler scope is resolved), so neither relies on a request-path tenant: each
/// carries the tenant explicitly, and Postgres RLS scopes the statement to it as well.
/// </summary>
public interface IUserLookupRepository
{
    /// <summary>
    /// The tenant's active users whose role is one of <paramref name="roles"/> — the audience
    /// of a compliance alert. Ordered by email so a dispatch's recipient list is stable across
    /// redeliveries. Empty when nobody in the tenant holds any of the roles.
    /// </summary>
    Task<IReadOnlyList<UserLookup>> ListActiveByRolesAsync(
        Guid tenantId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    /// <summary>Insert or update keyed on (UserId, TenantId) — idempotent under at-least-once delivery.</summary>
    Task UpsertAsync(UserLookup user, CancellationToken cancellationToken = default);
}
