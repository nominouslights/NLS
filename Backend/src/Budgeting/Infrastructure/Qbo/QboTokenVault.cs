namespace NorthernLink.Budgeting.Infrastructure.Qbo;

/// <summary>
/// One row of <c>budgeting.qbo_token_vault</c>: the tenant's tokens, each sealed by
/// <see cref="AesGcmQboTokenProtector"/>. A plain table, deliberately <b>not</b> an aggregate:
/// aggregates are snapshotted in full into <c>aggregate_snapshots</c> on every save, and an
/// encrypted token copied into an append-only audit table is a token that can never be deleted.
/// Its RLS policy is tenant-only, with no <c>app.is_system</c> bypass, so no tenant-less worker
/// session can read it.
/// </summary>
public sealed class QboTokenVaultEntry
{
    public Guid TenantId { get; set; }
    public string RealmId { get; set; } = null!;
    public string AccessTokenCipher { get; set; } = null!;
    public string RefreshTokenCipher { get; set; } = null!;
    public DateTimeOffset AccessTokenExpiresAtUtc { get; set; }
    public DateTimeOffset RefreshTokenExpiresAtUtc { get; set; }

    /// <summary>Which key sealed the two ciphers (also inside each cipher; here for operators checking a rotation).</summary>
    public string KeyId { get; set; } = null!;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>
/// Storage for <see cref="QboTokenVaultEntry"/> rows. Every write is a single SQL statement that
/// bypasses the EF change tracker, so writing the vault never flushes — and never commits —
/// unrelated tracked changes sharing the module's DbContext.
/// </summary>
public interface IQboTokenVault
{
    Task<QboTokenVaultEntry?> FindAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Inserts the row, or replaces every column of the tenant's existing one.</summary>
    Task UpsertAsync(QboTokenVaultEntry entry, CancellationToken cancellationToken);

    Task DeleteAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a transaction and locks the tenant's row (<c>SELECT … FOR UPDATE</c>), so two
    /// concurrent refreshes serialize: the second waits, then sees the first one's rotated token
    /// instead of spending the old one. Disposing without committing rolls back.
    /// </summary>
    Task<IQboTokenVaultLease> LockAsync(Guid tenantId, CancellationToken cancellationToken);
}

/// <summary>A locked vault row inside an open transaction. See <see cref="IQboTokenVault.LockAsync"/>.</summary>
public interface IQboTokenVaultLease : IAsyncDisposable
{
    /// <summary>The locked row, or null when the tenant has none.</summary>
    QboTokenVaultEntry? Entry { get; }

    /// <summary>Writes <paramref name="entry"/> over the locked row and commits.</summary>
    Task SaveAndCommitAsync(QboTokenVaultEntry entry, CancellationToken cancellationToken);

    /// <summary>Commits without writing, releasing the lock.</summary>
    Task CommitAsync(CancellationToken cancellationToken);
}
