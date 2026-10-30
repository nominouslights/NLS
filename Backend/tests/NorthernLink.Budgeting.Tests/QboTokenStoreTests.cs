using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Budgeting.Application.Qbo;
using NorthernLink.Budgeting.Domain.Qbo;
using NorthernLink.Budgeting.Infrastructure.Qbo;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// The token store's refresh path over a recording vault. What a unit test can prove without
/// Postgres: the order of lock → write → commit → hand-out, that a failed commit hands out
/// nothing, and that <c>invalid_grant</c> or an undecryptable cipher flips the connection to
/// NeedsReconnect. (That the lock really serializes two processes is the database's job — the
/// <c>FOR UPDATE</c> in <c>PostgresQboTokenVault</c>.)
/// </summary>
public class QboTokenStoreTests
{
    private readonly RecordingQboTokenVault _vault = new();
    private readonly AesGcmQboTokenProtector _protector;
    private readonly StubQboAuthClient _auth = new();
    private readonly InMemoryQboConnectionRepository _connections = new();
    private readonly FakeClock _clock = new(TestQbo.Now);
    private readonly QboTokenStore _store;

    public QboTokenStoreTests()
    {
        var key = TestQbo.NewKey();
        _protector = new AesGcmQboTokenProtector(name => name == QboSecrets.TokenKeyVariable ? key : null);
        _store = new QboTokenStore(_vault, _protector, _auth, _connections, _clock, NullLogger<QboTokenStore>.Instance);
        _connections.Add(TestQbo.Connection());
    }

    private QboConnection Connection => _connections.Connections[0];

    private Task StoreAsync(QboTokenGrant grant) =>
        _store.StoreGrantAsync(TestBudgeting.TenantId, TestQbo.RealmId, grant, CancellationToken.None);

    [Fact]
    public async Task Stored_tokens_are_encrypted_in_the_vault()
    {
        await StoreAsync(TestQbo.Grant("1"));

        var row = _vault.Rows[TestBudgeting.TenantId];
        Assert.DoesNotContain("access-1", row.AccessTokenCipher, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-1", row.RefreshTokenCipher, StringComparison.Ordinal);
        Assert.Equal(_protector.CurrentKeyId, row.KeyId);
        Assert.Equal(TestQbo.RealmId, row.RealmId);
    }

    [Fact]
    public async Task A_fresh_access_token_is_returned_without_refreshing()
    {
        await StoreAsync(TestQbo.Grant("1"));
        _vault.Log.Clear();

        var token = await _store.GetAccessTokenAsync(TestBudgeting.TenantId, CancellationToken.None);

        Assert.Equal("access-1", token);
        Assert.Empty(_auth.Refreshed);
        Assert.Equal(["lock", "commit", "release"], _vault.Log);
    }

    [Fact]
    public async Task A_near_expiry_token_is_refreshed_and_the_rotation_is_committed_before_it_is_used()
    {
        await StoreAsync(TestQbo.Grant("1"));
        _clock.Now = TestQbo.Now.AddMinutes(56); // 4 minutes left, inside the 5-minute margin
        _auth.OnRefresh = _ => TestQbo.Grant("2", _clock.Now);
        _vault.Log.Clear();

        var token = await _store.GetAccessTokenAsync(TestBudgeting.TenantId, CancellationToken.None);

        Assert.Equal("access-2", token);
        Assert.Equal(["refresh-1"], _auth.Refreshed);
        Assert.Equal(["lock", "write+commit", "release"], _vault.Log);

        // The vault now holds the ROTATED refresh token, so the next refresh spends the new one.
        var row = _vault.Rows[TestBudgeting.TenantId];
        Assert.Equal("refresh-2", _protector.Unprotect(row.RefreshTokenCipher, TestBudgeting.TenantId, TestQbo.RealmId));
    }

    [Fact]
    public async Task If_the_rotation_cannot_be_committed_no_access_token_is_handed_out()
    {
        await StoreAsync(TestQbo.Grant("1"));
        _clock.Now = TestQbo.Now.AddHours(2);
        _vault.FailCommit = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.GetAccessTokenAsync(TestBudgeting.TenantId, CancellationToken.None));

        Assert.Contains("rollback", _vault.Log);
    }

    [Fact]
    public async Task A_refresh_that_moves_the_expiry_a_day_updates_the_connection()
    {
        await StoreAsync(TestQbo.Grant("1"));
        _clock.Now = TestQbo.Now.AddDays(2);
        _auth.OnRefresh = _ => TestQbo.Grant("2", _clock.Now);

        await _store.GetAccessTokenAsync(TestBudgeting.TenantId, CancellationToken.None);

        Assert.Equal(_clock.Now.AddDays(100), Connection.RefreshTokenExpiresAtUtc);
        Assert.Equal(1, _connections.SaveChangesCallCount);
    }

    [Fact]
    public async Task Invalid_grant_marks_the_connection_NeedsReconnect_and_throws()
    {
        await StoreAsync(TestQbo.Grant("1"));
        _clock.Now = TestQbo.Now.AddHours(2);
        _auth.OnRefresh = _ => throw new QboAuthException(QboAuthFailure.InvalidGrant, "Intuit token refresh failed with HTTP 400 (invalid_grant).");
        _vault.Log.Clear();

        var exception = await Assert.ThrowsAsync<QboReconnectRequiredException>(
            () => _store.GetAccessTokenAsync(TestBudgeting.TenantId, CancellationToken.None));

        Assert.Equal("invalid_grant", exception.ErrorCode);
        Assert.Equal(QboConnectionStatus.NeedsReconnect, Connection.Status);
        Assert.Equal("invalid_grant", Connection.LastErrorCode);
        Assert.Equal(1, _connections.SaveChangesCallCount);

        // The lock was released (rolled back) before the connection was saved.
        Assert.Equal(["lock", "rollback"], _vault.Log);
    }

    [Fact]
    public async Task An_unreachable_Intuit_rolls_back_and_leaves_the_connection_alone()
    {
        await StoreAsync(TestQbo.Grant("1"));
        _clock.Now = TestQbo.Now.AddHours(2);
        _auth.OnRefresh = _ => throw new QboAuthException(QboAuthFailure.Unreachable, "Intuit token refresh could not be reached.");

        await Assert.ThrowsAsync<QboAuthException>(
            () => _store.GetAccessTokenAsync(TestBudgeting.TenantId, CancellationToken.None));

        Assert.Equal(QboConnectionStatus.Active, Connection.Status);
        Assert.Equal("refresh-1", _protector.Unprotect(
            _vault.Rows[TestBudgeting.TenantId].RefreshTokenCipher, TestBudgeting.TenantId, TestQbo.RealmId));
    }

    [Fact]
    public async Task No_stored_tokens_is_a_reconnect()
    {
        var exception = await Assert.ThrowsAsync<QboReconnectRequiredException>(
            () => _store.GetAccessTokenAsync(TestBudgeting.TenantId, CancellationToken.None));

        Assert.Equal("no_tokens", exception.ErrorCode);
        Assert.Equal(QboConnectionStatus.NeedsReconnect, Connection.Status);
    }

    [Fact]
    public async Task A_cipher_that_no_longer_decrypts_is_a_reconnect()
    {
        await StoreAsync(TestQbo.Grant("1"));
        var row = _vault.Rows[TestBudgeting.TenantId];
        row.AccessTokenCipher = row.AccessTokenCipher[..^4] + "AAAA";

        var exception = await Assert.ThrowsAsync<QboReconnectRequiredException>(
            () => _store.GetAccessTokenAsync(TestBudgeting.TenantId, CancellationToken.None));

        Assert.Equal("token_undecryptable", exception.ErrorCode);
        Assert.Equal(QboConnectionStatus.NeedsReconnect, Connection.Status);
    }

    [Fact]
    public async Task ReadRefreshToken_returns_null_rather_than_throwing_for_an_undecryptable_row()
    {
        await StoreAsync(TestQbo.Grant("1"));
        Assert.Equal("refresh-1", await _store.ReadRefreshTokenAsync(TestBudgeting.TenantId, CancellationToken.None));

        var row = _vault.Rows[TestBudgeting.TenantId];
        row.RefreshTokenCipher = row.RefreshTokenCipher[..^4] + "AAAA";
        Assert.Null(await _store.ReadRefreshTokenAsync(TestBudgeting.TenantId, CancellationToken.None));
    }
}
