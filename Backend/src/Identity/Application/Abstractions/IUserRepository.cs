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
