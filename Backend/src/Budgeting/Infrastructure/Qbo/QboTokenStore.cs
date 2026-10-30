using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Qbo;

namespace NorthernLink.Budgeting.Infrastructure.Qbo;

/// <summary>
/// <see cref="IQboTokenStore"/> over the encrypted vault. The refresh path is the part that has
/// to be right, because Intuit rotates refresh tokens and a spent one is gone for good:
/// <list type="number">
/// <item><description>Lock the tenant's vault row, so concurrent callers serialize on it.</description></item>
/// <item><description>If the access token has more than <see cref="RefreshMargin"/> left, use it.</description></item>
/// <item><description>Otherwise refresh, and <b>commit the new pair before returning the access
/// token</b>. If the commit fails, the caller gets the exception, not the token — using a token
/// whose refresh partner was never saved would strand the connection at the next refresh.</description></item>
/// <item><description><c>invalid_grant</c>, a missing row, or a cipher that no longer decrypts:
/// release the lock, mark the live connection NeedsReconnect, throw
/// <see cref="QboReconnectRequiredException"/>.</description></item>
/// </list>
/// Any other Intuit failure (unreachable, throttled) rolls back and propagates: the stored tokens
/// are still good, and the next attempt will retry.
/// </summary>
public sealed class QboTokenStore(
    IQboTokenVault vault,
    IQboTokenProtector protector,
    IQboAuthClient authClient,
    IQboConnectionRepository connections,
    TimeProvider clock,
    ILogger<QboTokenStore> logger) : IQboTokenStore
{
    /// <summary>An access token closer than this to expiry is refreshed rather than used.</summary>
    public static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    public Task StoreGrantAsync(Guid tenantId, string realmId, QboTokenGrant grant, CancellationToken cancellationToken) =>
        vault.UpsertAsync(Seal(tenantId, realmId, grant), cancellationToken);

    public async Task<string> GetAccessTokenAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        string? accessToken = null;
        string? reconnectCode = null;
        DateTimeOffset? renewedRefreshExpiry = null;

        // The lease is disposed — lock released, transaction closed — before anything below
        // touches the connection aggregate on the same DbContext.
        await using (var lease = await vault.LockAsync(tenantId, cancellationToken))
        {
            if (lease.Entry is not { } entry)
            {
                reconnectCode = "no_tokens";
            }
            else
            {
                try
                {
                    if (entry.AccessTokenExpiresAtUtc - clock.GetUtcNow() > RefreshMargin)
                    {
                        accessToken = protector.Unprotect(entry.AccessTokenCipher, tenantId, entry.RealmId);
                        await lease.CommitAsync(cancellationToken);
                    }
                    else
                    {
                        var refreshToken = protector.Unprotect(entry.RefreshTokenCipher, tenantId, entry.RealmId);

                        QboTokenGrant? grant = null;
                        try
                        {
                            grant = await authClient.RefreshAsync(refreshToken, cancellationToken);
                        }
                        catch (QboAuthException exception) when (exception.Failure == QboAuthFailure.InvalidGrant)
                        {
                            reconnectCode = "invalid_grant";
                        }

                        if (grant is not null)
                        {
                            // Persisted BEFORE use. Throws (and the token is never returned) if it fails.
                            await lease.SaveAndCommitAsync(Seal(tenantId, entry.RealmId, grant), cancellationToken);
                            accessToken = grant.AccessToken;
                            renewedRefreshExpiry = grant.RefreshTokenExpiresAtUtc;
                        }
                    }
                }
                catch (CryptographicException)
                {
                    // Not chained or logged: the exception says nothing useful beyond "cannot decrypt".
                    reconnectCode = "token_undecryptable";
                }
            }
        }

        if (reconnectCode is not null)
        {
            await MarkNeedsReconnectAsync(tenantId, reconnectCode, cancellationToken);
            throw new QboReconnectRequiredException(reconnectCode);
        }

        if (renewedRefreshExpiry is { } expiry
            && await connections.GetLiveAsync(cancellationToken) is { } connection
            && connection.NoteRefreshTokenExpiry(expiry, clock.GetUtcNow()))
        {
            await connections.SaveChangesAsync(cancellationToken);
        }

        return accessToken!;
    }

    public async Task<string?> ReadRefreshTokenAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var entry = await vault.FindAsync(tenantId, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        try
        {
            return protector.Unprotect(entry.RefreshTokenCipher, tenantId, entry.RealmId);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public Task DeleteAsync(Guid tenantId, CancellationToken cancellationToken) =>
        vault.DeleteAsync(tenantId, cancellationToken);

    private QboTokenVaultEntry Seal(Guid tenantId, string realmId, QboTokenGrant grant) => new()
    {
        TenantId = tenantId,
        RealmId = realmId,
        AccessTokenCipher = protector.Protect(grant.AccessToken, tenantId, realmId),
        RefreshTokenCipher = protector.Protect(grant.RefreshToken, tenantId, realmId),
        AccessTokenExpiresAtUtc = grant.AccessTokenExpiresAtUtc,
        RefreshTokenExpiresAtUtc = grant.RefreshTokenExpiresAtUtc,
        KeyId = protector.CurrentKeyId,
        UpdatedAtUtc = clock.GetUtcNow(),
    };

    private async Task MarkNeedsReconnectAsync(Guid tenantId, string errorCode, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "QuickBooks tokens for tenant {TenantId} are unusable ({ErrorCode}); the connection needs reconnecting",
            tenantId, errorCode);

        var connection = await connections.GetLiveAsync(cancellationToken);
        if (connection is null)
        {
            return;
        }

        var before = connection.Status;
        connection.MarkNeedsReconnect(errorCode, clock.GetUtcNow());
        if (connection.Status != before)
        {
            await connections.SaveChangesAsync(cancellationToken);
        }
    }
}
