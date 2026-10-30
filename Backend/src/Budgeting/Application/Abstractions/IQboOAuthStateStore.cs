using NorthernLink.Budgeting.Application.Qbo;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Persistence for <see cref="QboOAuthState"/>. Reads are tenant-scoped (query filter + RLS), so
/// another tenant's state reads back as null — indistinguishable from one that never existed.
/// </summary>
public interface IQboOAuthStateStore
{
    Task AddAsync(QboOAuthState state, CancellationToken cancellationToken);

    Task<QboOAuthState?> FindAsync(string stateHash, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the state consumed if nobody has yet — one conditional UPDATE, so of two concurrent
    /// callbacks exactly one wins. Returns whether this call consumed it.
    /// </summary>
    Task<bool> TryConsumeAsync(string stateHash, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Removes the tenant's states that expired before <paramref name="now"/>.</summary>
    Task PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
