using NorthernLink.Identity.Domain.Users;

namespace NorthernLink.Identity.Application.Abstractions;

/// <summary>
/// Write-side persistence for the User aggregate. <see cref="GetByEmailAsync"/> and
/// <see cref="GetByIdAsync"/> back the anonymous login/refresh flows, which run before any
/// tenant is known from an access token — implementations must bypass the normal
/// tenant-scoped query filter for those two lookups (see
/// <c>Infrastructure/Persistence/UserRepository.cs</c> for how RLS stays enforced anyway).
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// A user of the <b>current tenant</b>, by id — the signed-in read/write path behind the
    /// self-service profile. Unlike <see cref="GetByIdAsync"/> this deliberately does <i>not</i>
    /// bypass the tenant query filter and does <i>not</i> opt into the <c>app.is_system</c> RLS
    /// escape hatch: a caller holding an access token has a tenant, so both halves of dual
    /// enforcement apply normally and a cross-tenant id simply returns null.
    /// <para>
    /// Keeping this separate rather than widening <see cref="GetByIdAsync"/> matters because
    /// <c>SystemAccess</c> is sticky — it opens the connection so <c>app.is_system</c> survives
    /// the rest of the unit of work. A profile write riding it would commit its audit, journal
    /// and outbox rows past their tenant insert policies via the system arm instead of the tenant
    /// arm: the right result for the wrong reason, and a genuine cross-tenant defect would be
    /// invisible.
    /// </para>
    /// <para>Tracked, because the profile command mutates and saves through it.</para>
    /// </summary>
    Task<User?> GetByIdForTenantAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when any user exists — backs the first-run setup-status check.</summary>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically creates the first admin: takes a transaction-scoped advisory lock, re-checks
    /// that no user exists, and only then inserts <paramref name="user"/>. Returns false without
    /// inserting if a user already exists — the one-time first-run window has closed. Serializes
    /// concurrent setup attempts so exactly one can win.
    /// </summary>
    Task<bool> TryAddFirstUserAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds <paramref name="user"/> and saves the whole pending unit of work in one commit.
    /// Returns false — persisting nothing, including any other pending changes on the same
    /// unit of work — when the email unique index rejects the insert. Naming mirrors
    /// <see cref="TryAddFirstUserAsync"/>: the concurrency-safe complement to a pre-check.
    /// </summary>
    Task<bool> TryAddNewUserAsync(User user, CancellationToken cancellationToken = default);

    void Add(User user);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
