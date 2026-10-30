using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Qbo;
using NorthernLink.Budgeting.Domain.Qbo;
using NorthernLink.Budgeting.Infrastructure.Qbo;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Write-side repository for <see cref="QboConnection"/> (tenant-filtered).</summary>
internal sealed class QboConnectionRepository(BudgetingDbContext context) : IQboConnectionRepository
{
    public async Task<IReadOnlyList<QboConnection>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.QboConnections.ToListAsync(cancellationToken);

    public Task<QboConnection?> GetLiveAsync(CancellationToken cancellationToken = default) =>
        context.QboConnections.FirstOrDefaultAsync(c => c.Status != QboConnectionStatus.Disconnected, cancellationToken);

    public void Add(QboConnection connection) => context.QboConnections.Add(connection);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// <see cref="IQboOAuthStateStore"/> over <c>budgeting.qbo_oauth_states</c>. Every read and the
/// consume go through the tenant query filter, and RLS (tenant-only, no system bypass) backs it:
/// a state started in another tenant is invisible here, so it can neither be read nor spent.
/// </summary>
internal sealed class QboOAuthStateStore(BudgetingDbContext context) : IQboOAuthStateStore
{
    public async Task AddAsync(QboOAuthState state, CancellationToken cancellationToken)
    {
        context.QboOAuthStates.Add(state);
        await context.SaveChangesAsync(cancellationToken);
        context.Entry(state).State = EntityState.Detached;
    }

    public Task<QboOAuthState?> FindAsync(string stateHash, CancellationToken cancellationToken) =>
        context.QboOAuthStates.AsNoTracking().FirstOrDefaultAsync(s => s.StateHash == stateHash, cancellationToken);

    public async Task<bool> TryConsumeAsync(string stateHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.QboOAuthStates
            .Where(s => s.StateHash == stateHash && s.ConsumedAtUtc == null && s.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.ConsumedAtUtc, now), cancellationToken) == 1;

    public Task PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        context.QboOAuthStates
            .Where(s => s.ExpiresAtUtc < now)
            .ExecuteDeleteAsync(cancellationToken);
}

/// <summary>
/// <see cref="IQboTokenVault"/> over <c>budgeting.qbo_token_vault</c>. Every statement names the
/// tenant explicitly rather than leaning on the query filter: the scheduled import (a later
/// slice) runs in a worker scope, and an explicit predicate reads the same in both. RLS —
/// tenant-only, no <c>app.is_system</c> policy — is the backstop either way. No write goes
/// through the change tracker (see <see cref="IQboTokenVault"/>).
/// </summary>
internal sealed class PostgresQboTokenVault(BudgetingDbContext context) : IQboTokenVault
{
    public Task<QboTokenVaultEntry?> FindAsync(Guid tenantId, CancellationToken cancellationToken) =>
        context.QboTokenVault
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId, cancellationToken);

    public async Task UpsertAsync(QboTokenVaultEntry entry, CancellationToken cancellationToken) =>
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO budgeting.qbo_token_vault
                (tenant_id, realm_id, access_token_cipher, refresh_token_cipher,
                 access_token_expires_at_utc, refresh_token_expires_at_utc, key_id, updated_at_utc)
            VALUES
                ({entry.TenantId}, {entry.RealmId}, {entry.AccessTokenCipher}, {entry.RefreshTokenCipher},
                 {entry.AccessTokenExpiresAtUtc}, {entry.RefreshTokenExpiresAtUtc}, {entry.KeyId}, {entry.UpdatedAtUtc})
            ON CONFLICT (tenant_id) DO UPDATE SET
                realm_id = EXCLUDED.realm_id,
                access_token_cipher = EXCLUDED.access_token_cipher,
                refresh_token_cipher = EXCLUDED.refresh_token_cipher,
                access_token_expires_at_utc = EXCLUDED.access_token_expires_at_utc,
                refresh_token_expires_at_utc = EXCLUDED.refresh_token_expires_at_utc,
                key_id = EXCLUDED.key_id,
                updated_at_utc = EXCLUDED.updated_at_utc
            """,
            cancellationToken);

    public Task DeleteAsync(Guid tenantId, CancellationToken cancellationToken) =>
        context.QboTokenVault
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<IQboTokenVaultLease> LockAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Uncomposed FromSql (ToList, not FirstOrDefault): EF sends the statement as written,
            // so the FOR UPDATE is never wrapped in a subquery.
            var rows = await context.QboTokenVault
                .FromSqlInterpolated($"SELECT * FROM budgeting.qbo_token_vault WHERE tenant_id = {tenantId} FOR UPDATE")
                .IgnoreQueryFilters()
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return new Lease(context, transaction, rows.SingleOrDefault());
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class Lease(BudgetingDbContext context, IDbContextTransaction transaction, QboTokenVaultEntry? entry)
        : IQboTokenVaultLease
    {
        public QboTokenVaultEntry? Entry { get; } = entry;

        public async Task SaveAndCommitAsync(QboTokenVaultEntry replacement, CancellationToken cancellationToken)
        {
            await context.QboTokenVault
                .IgnoreQueryFilters()
                .Where(e => e.TenantId == replacement.TenantId)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(e => e.RealmId, replacement.RealmId)
                        .SetProperty(e => e.AccessTokenCipher, replacement.AccessTokenCipher)
                        .SetProperty(e => e.RefreshTokenCipher, replacement.RefreshTokenCipher)
                        .SetProperty(e => e.AccessTokenExpiresAtUtc, replacement.AccessTokenExpiresAtUtc)
                        .SetProperty(e => e.RefreshTokenExpiresAtUtc, replacement.RefreshTokenExpiresAtUtc)
                        .SetProperty(e => e.KeyId, replacement.KeyId)
                        .SetProperty(e => e.UpdatedAtUtc, replacement.UpdatedAtUtc),
                    cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        // Rolls back when nothing was committed.
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
