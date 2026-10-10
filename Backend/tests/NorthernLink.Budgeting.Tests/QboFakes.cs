using System.Net;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Qbo;
using NorthernLink.Budgeting.Domain.Qbo;
using NorthernLink.Budgeting.Infrastructure.Qbo;

namespace NorthernLink.Budgeting.Tests;

/// <summary>A clock that stands still until a test moves it.</summary>
internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Shared QuickBooks test values.</summary>
internal static class TestQbo
{
    public static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    public const string RealmId = "9130351234567890";
    public const string OtherRealmId = "9130359999999999";

    public static readonly Guid OtherTenantId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    public static readonly Guid OtherUserId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    public static QboTokenGrant Grant(string suffix = "1", DateTimeOffset? at = null) => new(
        $"access-{suffix}",
        $"refresh-{suffix}",
        (at ?? Now).AddHours(1),
        (at ?? Now).AddDays(100));

    public static QboConnection Connection(
        string realmId = RealmId,
        Guid? tenantId = null,
        DateTimeOffset? at = null)
    {
        var result = QboConnection.Connect(
            tenantId ?? TestBudgeting.TenantId, realmId, "Northern Link Shuttle & Cargo", QboEnvironment.Sandbox,
            "CAD", TestBudgeting.ActorId, (at ?? Now).AddDays(100), at ?? Now);
        return result.Value;
    }

    /// <summary>A random valid vault key, base64.</summary>
    public static string NewKey() => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
}

/// <summary>In-memory <see cref="IQboConnectionRepository"/>, scoped to one tenant like the EF query filter.</summary>
internal sealed class InMemoryQboConnectionRepository : IQboConnectionRepository
{
    public Guid QueryFilterTenantId { get; init; } = TestBudgeting.TenantId;
    public List<QboConnection> Connections { get; } = [];
    public int SaveChangesCallCount { get; private set; }

    public Task<IReadOnlyList<QboConnection>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<QboConnection>>(Connections.Where(c => c.TenantId == QueryFilterTenantId).ToList());

    public Task<QboConnection?> GetLiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Connections.FirstOrDefault(c => c.TenantId == QueryFilterTenantId && c.IsLive));

    public void Add(QboConnection connection) => Connections.Add(connection);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory <see cref="IQboOAuthStateStore"/> mirroring the real one's tenant scoping: reads and
/// the consume see only <see cref="QueryFilterTenantId"/>'s rows.
/// </summary>
internal sealed class InMemoryQboOAuthStateStore : IQboOAuthStateStore
{
    public Guid QueryFilterTenantId { get; init; } = TestBudgeting.TenantId;
    public List<QboOAuthState> States { get; } = [];

    public Task AddAsync(QboOAuthState state, CancellationToken cancellationToken)
    {
        States.Add(state);
        return Task.CompletedTask;
    }

    public Task<QboOAuthState?> FindAsync(string stateHash, CancellationToken cancellationToken) =>
        Task.FromResult(Visible().FirstOrDefault(s => s.StateHash == stateHash));

    public Task<bool> TryConsumeAsync(string stateHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var state = Visible().FirstOrDefault(s => s.StateHash == stateHash && s.ConsumedAtUtc is null && s.ExpiresAtUtc > now);
        if (state is null)
        {
            return Task.FromResult(false);
        }

        state.ConsumedAtUtc = now;
        return Task.FromResult(true);
    }

    public Task PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        States.RemoveAll(s => s.TenantId == QueryFilterTenantId && s.ExpiresAtUtc < now);
        return Task.CompletedTask;
    }

    private IEnumerable<QboOAuthState> Visible() => States.Where(s => s.TenantId == QueryFilterTenantId);
}

/// <summary>Scriptable <see cref="IQboAuthClient"/> that records what it was asked.</summary>
internal sealed class StubQboAuthClient : IQboAuthClient
{
    public Func<string, QboTokenGrant> OnExchange { get; set; } = _ => TestQbo.Grant("exchanged");
    public Func<string, QboTokenGrant> OnRefresh { get; set; } = _ => TestQbo.Grant("refreshed");
    public Exception? RevokeFailure { get; set; }
    public bool NotConfigured { get; set; }

    public List<string> Exchanged { get; } = [];
    public List<string> Refreshed { get; } = [];
    public List<string> Revoked { get; } = [];

    public string BuildAuthorizeUrl(string state) => NotConfigured
        ? throw new QboAuthException(QboAuthFailure.NotConfigured, "not configured")
        : $"https://appcenter.intuit.com/connect/oauth2?state={state}";

    public Task<QboTokenGrant> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        Exchanged.Add(code);
        return Task.FromResult(OnExchange(code));
    }

    public Task<QboTokenGrant> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        Refreshed.Add(refreshToken);
        return Task.FromResult(OnRefresh(refreshToken));
    }

    public Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        Revoked.Add(token);
        return RevokeFailure is null ? Task.CompletedTask : Task.FromException(RevokeFailure);
    }
}

/// <summary>Scriptable <see cref="IQboAccountingClient"/>.</summary>
internal sealed class StubQboAccountingClient : IQboAccountingClient
{
    public string Environment { get; set; } = "Sandbox";
    public string CompanyName { get; set; } = "Northern Link Shuttle & Cargo";
    public string? HomeCurrency { get; set; } = "CAD";
    public Exception? Failure { get; set; }

    public Task<QboCompanyInfo> GetCompanyInfoAsync(string realmId, string accessToken, CancellationToken cancellationToken) =>
        Failure is null
            ? Task.FromResult(new QboCompanyInfo(CompanyName, "CA"))
            : Task.FromException<QboCompanyInfo>(Failure);

    public Task<string?> GetHomeCurrencyAsync(string realmId, string accessToken, CancellationToken cancellationToken) =>
        Failure is null ? Task.FromResult(HomeCurrency) : Task.FromException<string?>(Failure);
}

/// <summary>In-memory <see cref="IQboTokenStore"/> for handler tests — stores grants unencrypted, by tenant.</summary>
internal sealed class InMemoryQboTokenStore : IQboTokenStore
{
    public Dictionary<Guid, (string RealmId, QboTokenGrant Grant)> Grants { get; } = [];

    public Task StoreGrantAsync(Guid tenantId, string realmId, QboTokenGrant grant, CancellationToken cancellationToken)
    {
        Grants[tenantId] = (realmId, grant);
        return Task.CompletedTask;
    }

    public Task<string> GetAccessTokenAsync(Guid tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(Grants[tenantId].Grant.AccessToken);

    public Task<string?> ReadRefreshTokenAsync(Guid tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(Grants.TryGetValue(tenantId, out var entry) ? entry.Grant.RefreshToken : null);

    public Task DeleteAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        Grants.Remove(tenantId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory <see cref="IQboTokenVault"/> that logs every step, so a test can assert the order in
/// which the store locks, writes, commits and hands out a token.
/// </summary>
internal sealed class RecordingQboTokenVault : IQboTokenVault
{
    public Dictionary<Guid, QboTokenVaultEntry> Rows { get; } = [];
    public List<string> Log { get; } = [];
    public bool FailCommit { get; set; }

    public Task<QboTokenVaultEntry?> FindAsync(Guid tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(Rows.GetValueOrDefault(tenantId));

    public Task UpsertAsync(QboTokenVaultEntry entry, CancellationToken cancellationToken)
    {
        Log.Add("upsert");
        Rows[entry.TenantId] = entry;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        Log.Add("delete");
        Rows.Remove(tenantId);
        return Task.CompletedTask;
    }

    public Task<IQboTokenVaultLease> LockAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        Log.Add("lock");
        return Task.FromResult<IQboTokenVaultLease>(new Lease(this, Rows.GetValueOrDefault(tenantId)));
    }

    private sealed class Lease(RecordingQboTokenVault vault, QboTokenVaultEntry? entry) : IQboTokenVaultLease
    {
        private bool _committed;

        public QboTokenVaultEntry? Entry { get; } = entry;

        public Task SaveAndCommitAsync(QboTokenVaultEntry replacement, CancellationToken cancellationToken)
        {
            if (vault.FailCommit)
            {
                vault.Log.Add("commit-failed");
                throw new InvalidOperationException("simulated commit failure");
            }

            vault.Rows[replacement.TenantId] = replacement;
            vault.Log.Add("write+commit");
            _committed = true;
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            vault.Log.Add("commit");
            _committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            vault.Log.Add(_committed ? "release" : "rollback");
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>An <see cref="HttpMessageHandler"/> that answers from a script and keeps the requests it saw.</summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, body));
        return respond(request, body);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
    };
}
