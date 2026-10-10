using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Budgeting.Application.Integration;
using NorthernLink.Budgeting.Application.Qbo;
using NorthernLink.Budgeting.Application.Qbo.Authorize;
using NorthernLink.Budgeting.Application.Qbo.Complete;
using NorthernLink.Budgeting.Application.Qbo.Disconnect;
using NorthernLink.Budgeting.Application.Qbo.GetConnection;
using NorthernLink.Budgeting.Domain.Qbo;
using NorthernLink.Budgeting.Infrastructure.Qbo;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// The four connection handlers over in-memory fakes. The state checks are the security-relevant
/// part: a state is single use, expires, and is bound to the tenant and user that started it.
/// </summary>
public class QboConnectionHandlerTests
{
    private readonly InMemoryQboOAuthStateStore _states = new();
    private readonly InMemoryQboConnectionRepository _connections = new();
    private readonly StubQboAuthClient _auth = new();
    private readonly StubQboAccountingClient _accounting = new();
    private readonly InMemoryQboTokenStore _tokens = new();
    private readonly InMemoryUserLookupRepository _users = new();
    private readonly FakeClock _clock = new(TestQbo.Now);
    private readonly AesGcmQboTokenProtector _protector;

    public QboConnectionHandlerTests()
    {
        var key = TestQbo.NewKey();
        _protector = new AesGcmQboTokenProtector(name => name == QboSecrets.TokenKeyVariable ? key : null);
    }

    private StartQboAuthorizationCommandHandler StartHandler(IQboTokenProtector? protector = null) =>
        new(_auth, protector ?? _protector, _states, _clock);

    private CompleteQboConnectionCommandHandler CompleteHandler() =>
        new(_states, _connections, _auth, _accounting, _tokens, _clock,
            NullLogger<CompleteQboConnectionCommandHandler>.Instance);

    private DisconnectQboCommandHandler DisconnectHandler() =>
        new(_connections, _tokens, _auth, _clock, NullLogger<DisconnectQboCommandHandler>.Instance);

    /// <summary>Runs the authorize step and returns the raw state value Intuit would echo back.</summary>
    private async Task<string> StartAsync(Guid? userId = null)
    {
        var result = await StartHandler().Handle(
            new StartQboAuthorizationCommand(TestBudgeting.TenantId, userId ?? TestBudgeting.ActorId),
            CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value.Split("state=")[1];
    }

    private Task<Result> CompleteAsync(
        string state,
        string realmId = TestQbo.RealmId,
        Guid? userId = null,
        Guid? tenantId = null,
        string code = "auth-code") =>
        CompleteHandler().Handle(
            new CompleteQboConnectionCommand(
                tenantId ?? TestBudgeting.TenantId, userId ?? TestBudgeting.ActorId, code, state, realmId),
            CancellationToken.None);

    // ---- Authorize ----

    [Fact]
    public async Task Authorize_stores_only_the_hash_of_the_state_bound_to_tenant_and_user()
    {
        var state = await StartAsync();

        var stored = Assert.Single(_states.States);
        Assert.Equal(QboOAuthState.Hash(state), stored.StateHash);
        Assert.NotEqual(state, stored.StateHash);
        Assert.Equal(TestBudgeting.TenantId, stored.TenantId);
        Assert.Equal(TestBudgeting.ActorId, stored.UserId);
        Assert.Equal(TestQbo.Now.AddMinutes(10), stored.ExpiresAtUtc);
    }

    [Fact]
    public async Task Authorize_without_a_user_is_refused()
    {
        var result = await StartHandler().Handle(
            new StartQboAuthorizationCommand(TestBudgeting.TenantId, null), CancellationToken.None);

        Assert.Equal(QboConnectionErrors.UserRequired, result.Error);
        Assert.Empty(_states.States);
    }

    [Fact]
    public async Task Authorize_is_NotConfigured_without_client_credentials_and_stores_nothing()
    {
        _auth.NotConfigured = true;

        var result = await StartHandler().Handle(
            new StartQboAuthorizationCommand(TestBudgeting.TenantId, TestBudgeting.ActorId), CancellationToken.None);

        Assert.Equal(QboConnectionErrors.NotConfigured, result.Error);
        Assert.Empty(_states.States);
    }

    [Fact]
    public async Task Authorize_is_NotConfigured_without_a_vault_key()
    {
        var keyless = new AesGcmQboTokenProtector(_ => null);

        var result = await StartHandler(keyless).Handle(
            new StartQboAuthorizationCommand(TestBudgeting.TenantId, TestBudgeting.ActorId), CancellationToken.None);

        Assert.Equal(QboConnectionErrors.NotConfigured, result.Error);
        Assert.Empty(_states.States);
    }

    [Fact]
    public async Task Authorize_sweeps_expired_states()
    {
        await StartAsync();
        _clock.Now = TestQbo.Now.AddMinutes(30);

        await StartAsync();

        Assert.Single(_states.States);
    }

    // ---- Complete: the state ----

    [Fact]
    public async Task Complete_connects_stores_the_tokens_and_spends_the_state()
    {
        var state = await StartAsync();

        var result = await CompleteAsync(state);

        Assert.True(result.IsSuccess);
        var connection = Assert.Single(_connections.Connections);
        Assert.Equal(QboConnectionStatus.Active, connection.Status);
        Assert.Equal(TestQbo.RealmId, connection.RealmId);
        Assert.Equal(QboEnvironment.Sandbox, connection.Environment);
        Assert.Equal("refresh-exchanged", _tokens.Grants[TestBudgeting.TenantId].Grant.RefreshToken);
        Assert.Equal(["auth-code"], _auth.Exchanged);
        Assert.NotNull(Assert.Single(_states.States).ConsumedAtUtc);
        Assert.Equal(1, _connections.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_reused_state_is_invalid()
    {
        var state = await StartAsync();
        Assert.True((await CompleteAsync(state)).IsSuccess);

        var again = await CompleteAsync(state);

        Assert.Equal(QboConnectionErrors.StateInvalid, again.Error);
        Assert.Single(_auth.Exchanged);
    }

    [Fact]
    public async Task An_expired_state_is_StateExpired()
    {
        var state = await StartAsync();
        _clock.Now = TestQbo.Now.AddMinutes(10);

        var result = await CompleteAsync(state);

        Assert.Equal(QboConnectionErrors.StateExpired, result.Error);
        Assert.Empty(_auth.Exchanged);
    }

    [Fact]
    public async Task A_state_started_by_another_user_is_invalid()
    {
        var state = await StartAsync();

        var result = await CompleteAsync(state, userId: TestQbo.OtherUserId);

        Assert.Equal(QboConnectionErrors.StateInvalid, result.Error);
        Assert.Null(Assert.Single(_states.States).ConsumedAtUtc);
        Assert.Empty(_auth.Exchanged);
    }

    [Fact]
    public async Task A_state_started_in_another_tenant_is_invalid()
    {
        var state = await StartAsync();

        // The other tenant's store cannot see this tenant's state (query filter + RLS) …
        var otherTenantStates = new InMemoryQboOAuthStateStore { QueryFilterTenantId = TestQbo.OtherTenantId };
        otherTenantStates.States.AddRange(_states.States);
        var handler = new CompleteQboConnectionCommandHandler(
            otherTenantStates, new InMemoryQboConnectionRepository { QueryFilterTenantId = TestQbo.OtherTenantId },
            _auth, _accounting, _tokens, _clock, NullLogger<CompleteQboConnectionCommandHandler>.Instance);

        var result = await handler.Handle(
            new CompleteQboConnectionCommand(TestQbo.OtherTenantId, TestBudgeting.ActorId, "c", state, TestQbo.RealmId),
            CancellationToken.None);

        Assert.Equal(QboConnectionErrors.StateInvalid, result.Error);

        // … and even if a store leaked it, the handler compares the tenant itself.
        var leaky = await CompleteAsync(state, tenantId: TestQbo.OtherTenantId);
        Assert.Equal(QboConnectionErrors.StateInvalid, leaky.Error);
        Assert.Empty(_auth.Exchanged);
    }

    [Fact]
    public async Task An_unknown_state_is_invalid()
    {
        await StartAsync();

        Assert.Equal(QboConnectionErrors.StateInvalid, (await CompleteAsync("made-up")).Error);
    }

    [Theory]
    [InlineData(null, "s", TestQbo.RealmId)]
    [InlineData("c", null, TestQbo.RealmId)]
    [InlineData("c", "s", null)]
    [InlineData("c", "s", " ")]
    public async Task A_callback_missing_a_value_is_incomplete(string? code, string? state, string? realmId)
    {
        var result = await CompleteHandler().Handle(
            new CompleteQboConnectionCommand(TestBudgeting.TenantId, TestBudgeting.ActorId, code, state, realmId),
            CancellationToken.None);

        Assert.Equal(QboConnectionErrors.CallbackIncomplete, result.Error);
    }

    [Fact]
    public async Task A_realm_id_that_is_not_digits_is_refused_before_the_state_is_spent()
    {
        var state = await StartAsync();

        var result = await CompleteAsync(state, realmId: "123abc");

        Assert.Equal(QboConnectionErrors.RealmIdInvalid, result.Error);
        Assert.Null(Assert.Single(_states.States).ConsumedAtUtc);
    }

    // ---- Complete: the company ----

    [Fact]
    public async Task A_non_CAD_company_is_refused_and_its_grant_revoked()
    {
        _accounting.HomeCurrency = "USD";
        var state = await StartAsync();

        var result = await CompleteAsync(state);

        Assert.Equal(QboConnectionErrors.HomeCurrencyNotCad, result.Error);
        Assert.Empty(_connections.Connections);
        Assert.Empty(_tokens.Grants);
        Assert.Equal(["refresh-exchanged"], _auth.Revoked);
    }

    [Fact]
    public async Task A_different_company_is_refused_before_any_token_is_requested()
    {
        _connections.Add(TestQbo.Connection(TestQbo.RealmId));
        var state = await StartAsync();

        var result = await CompleteAsync(state, realmId: TestQbo.OtherRealmId);

        Assert.Equal(QboConnectionErrors.DifferentCompany, result.Error);
        Assert.Empty(_auth.Exchanged);
        Assert.Single(_connections.Connections);
    }

    [Fact]
    public async Task A_different_company_is_refused_even_after_the_first_was_disconnected()
    {
        var old = TestQbo.Connection(TestQbo.RealmId);
        old.Disconnect(TestBudgeting.ActorId, TestQbo.Now);
        _connections.Add(old);
        var state = await StartAsync();

        var result = await CompleteAsync(state, realmId: TestQbo.OtherRealmId);

        Assert.Equal(QboConnectionErrors.DifferentCompany, result.Error);
    }

    [Fact]
    public async Task Reconnecting_the_same_company_revives_its_row()
    {
        var existing = TestQbo.Connection(TestQbo.RealmId);
        existing.MarkNeedsReconnect("invalid_grant", TestQbo.Now);
        _connections.Add(existing);
        var state = await StartAsync();

        var result = await CompleteAsync(state);

        Assert.True(result.IsSuccess);
        Assert.Same(existing, Assert.Single(_connections.Connections));
        Assert.Equal(QboConnectionStatus.Active, existing.Status);
    }

    [Fact]
    public async Task A_refused_token_exchange_is_TokenExchangeFailed()
    {
        _auth.OnExchange = _ => throw new QboAuthException(QboAuthFailure.InvalidGrant, "Intuit code exchange failed with HTTP 400 (invalid_grant).");
        var state = await StartAsync();

        var result = await CompleteAsync(state);

        Assert.Equal(QboConnectionErrors.TokenExchangeFailed, result.Error);
        Assert.Contains(result.Error.Code, QboConnectionErrors.UpstreamCodes);
        Assert.Empty(_connections.Connections);
    }

    [Fact]
    public async Task A_failed_company_lookup_is_CompanyLookupFailed_and_revokes()
    {
        _accounting.Failure = new QboApiException(QboApiFailure.Unreachable, "QuickBooks companyinfo could not be reached.");
        var state = await StartAsync();

        var result = await CompleteAsync(state);

        Assert.Equal(QboConnectionErrors.CompanyLookupFailed, result.Error);
        Assert.Equal(["refresh-exchanged"], _auth.Revoked);
        Assert.Empty(_tokens.Grants);
    }

    // ---- Disconnect ----

    [Fact]
    public async Task Disconnect_revokes_deletes_the_tokens_and_keeps_the_row()
    {
        var state = await StartAsync();
        await CompleteAsync(state);

        var result = await DisconnectHandler().Handle(
            new DisconnectQboCommand(TestBudgeting.TenantId, TestBudgeting.ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["refresh-exchanged"], _auth.Revoked);
        Assert.Empty(_tokens.Grants);
        Assert.Equal(QboConnectionStatus.Disconnected, Assert.Single(_connections.Connections).Status);
    }

    [Fact]
    public async Task Disconnect_still_disconnects_when_Intuit_refuses_the_revoke()
    {
        var state = await StartAsync();
        await CompleteAsync(state);
        _auth.RevokeFailure = new QboAuthException(QboAuthFailure.Unreachable, "Intuit revoke could not be reached.");

        var result = await DisconnectHandler().Handle(
            new DisconnectQboCommand(TestBudgeting.TenantId, TestBudgeting.ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_tokens.Grants);
        Assert.False(Assert.Single(_connections.Connections).IsLive);
    }

    [Fact]
    public async Task Disconnect_with_nothing_connected_is_NotConnected()
    {
        var result = await DisconnectHandler().Handle(
            new DisconnectQboCommand(TestBudgeting.TenantId, TestBudgeting.ActorId), CancellationToken.None);

        Assert.Equal(QboConnectionErrors.NotConnected, result.Error);
    }

    // ---- Get ----

    [Fact]
    public async Task Get_with_no_history_is_NotConnected()
    {
        var result = await new GetQboConnectionQueryHandler(_connections, _users).Handle(
            new GetQboConnectionQuery(TestBudgeting.TenantId), CancellationToken.None);

        Assert.Equal("NotConnected", result.Value.Status);
        Assert.Null(result.Value.CompanyName);
    }

    [Fact]
    public async Task Get_names_who_connected_it()
    {
        _users.Users.Add(new UserLookup
        {
            UserId = TestBudgeting.ActorId,
            TenantId = TestBudgeting.TenantId,
            Email = "owner@northernlink.test",
            FullName = "Pat Owner",
            Role = Roles.Owner,
            UpdatedAtUtc = TestQbo.Now,
        });
        _connections.Add(TestQbo.Connection());

        var response = (await new GetQboConnectionQueryHandler(_connections, _users).Handle(
            new GetQboConnectionQuery(TestBudgeting.TenantId), CancellationToken.None)).Value;

        Assert.Equal("Active", response.Status);
        Assert.Equal("Sandbox", response.Environment);
        Assert.Equal("Pat Owner", response.ConnectedByName);
        Assert.Equal("owner@northernlink.test", response.ConnectedByEmail);
        Assert.Equal(TestQbo.Now.AddDays(100), response.RefreshTokenExpiresAtUtc);
        Assert.Null(response.LastSyncAtUtc);
    }

    [Fact]
    public async Task Get_after_a_disconnect_reports_Disconnected_without_an_expiry()
    {
        var connection = TestQbo.Connection();
        connection.Disconnect(TestBudgeting.ActorId, TestQbo.Now);
        _connections.Add(connection);

        var response = (await new GetQboConnectionQueryHandler(_connections, _users).Handle(
            new GetQboConnectionQuery(TestBudgeting.TenantId), CancellationToken.None)).Value;

        Assert.Equal("Disconnected", response.Status);
        Assert.Null(response.RefreshTokenExpiresAtUtc);
    }
}
