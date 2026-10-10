using NorthernLink.Budgeting.Domain.Qbo;
using NorthernLink.Budgeting.Domain.Qbo.Events;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>The QboConnection aggregate's own rules: CAD only, one company, status moves, and no token anywhere.</summary>
public class QboConnectionTests
{
    private static readonly DateTimeOffset Now = TestQbo.Now;

    [Fact]
    public void Connecting_a_CAD_company_is_Active_and_raises_Connected()
    {
        var connection = TestQbo.Connection();

        Assert.Equal(QboConnectionStatus.Active, connection.Status);
        Assert.Equal(TestQbo.RealmId, connection.RealmId);
        Assert.Equal(TestBudgeting.ActorId, connection.ConnectedBy);
        Assert.Equal(Now, connection.ConnectedAtUtc);
        Assert.Equal(Now.AddDays(100), connection.RefreshTokenExpiresAtUtc);
        Assert.Null(connection.LastSuccessfulSyncAtUtc);

        var connected = Assert.Single(connection.DomainEvents.OfType<QboConnectedDomainEvent>());
        Assert.False(connected.IsReconnect);
        Assert.Equal(TestQbo.RealmId, connected.RealmId);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("")]
    [InlineData(null)]
    public void A_home_currency_other_than_CAD_is_refused(string? homeCurrency)
    {
        var result = QboConnection.Connect(
            TestBudgeting.TenantId, TestQbo.RealmId, "Acme", QboEnvironment.Sandbox, homeCurrency,
            TestBudgeting.ActorId, Now.AddDays(100), Now);

        Assert.True(result.IsFailure);
        Assert.Equal(QboConnectionErrors.HomeCurrencyNotCad, result.Error);
    }

    [Theory]
    [InlineData("cad")]
    [InlineData(" CAD ")]
    public void CAD_is_matched_case_insensitively_and_trimmed(string homeCurrency)
    {
        var result = QboConnection.Connect(
            TestBudgeting.TenantId, TestQbo.RealmId, "Acme", QboEnvironment.Sandbox, homeCurrency,
            TestBudgeting.ActorId, Now.AddDays(100), Now);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12ab")]
    [InlineData("123/../456")]
    [InlineData("123456789012345678901234567890123")] // 33 digits
    public void A_realm_id_that_is_not_digits_is_refused(string realmId)
    {
        var result = QboConnection.Connect(
            TestBudgeting.TenantId, realmId, "Acme", QboEnvironment.Sandbox, "CAD",
            TestBudgeting.ActorId, Now.AddDays(100), Now);

        Assert.Equal(QboConnectionErrors.RealmIdInvalid, result.Error);
    }

    [Fact]
    public void EnsureSameCompany_passes_with_no_history_or_the_same_realm()
    {
        Assert.True(QboConnection.EnsureSameCompany([], TestQbo.RealmId).IsSuccess);
        Assert.True(QboConnection.EnsureSameCompany([TestQbo.Connection()], TestQbo.RealmId).IsSuccess);
    }

    [Fact]
    public void EnsureSameCompany_refuses_a_different_realm_even_when_the_old_one_is_disconnected()
    {
        var old = TestQbo.Connection();
        old.Disconnect(TestBudgeting.ActorId, Now);

        var result = QboConnection.EnsureSameCompany([old], TestQbo.OtherRealmId);

        Assert.Equal(QboConnectionErrors.DifferentCompany, result.Error);
    }

    [Fact]
    public void Reconnect_to_a_different_realm_is_refused()
    {
        var connection = TestQbo.Connection();

        var result = connection.Reconnect(
            TestQbo.OtherRealmId, "Other", QboEnvironment.Sandbox, "CAD", TestBudgeting.ActorId, Now.AddDays(100), Now);

        Assert.Equal(QboConnectionErrors.DifferentCompany, result.Error);
        Assert.Equal(TestQbo.RealmId, connection.RealmId);
    }

    [Fact]
    public void Reconnect_revives_a_NeedsReconnect_connection_and_clears_the_error()
    {
        var connection = TestQbo.Connection();
        connection.MarkNeedsReconnect("invalid_grant", Now);
        connection.ClearDomainEvents();

        var later = Now.AddDays(3);
        var result = connection.Reconnect(
            TestQbo.RealmId, "Renamed Co", QboEnvironment.Sandbox, "CAD", TestQbo.OtherUserId, later.AddDays(100), later);

        Assert.True(result.IsSuccess);
        Assert.Equal(QboConnectionStatus.Active, connection.Status);
        Assert.Null(connection.LastErrorCode);
        Assert.Equal("Renamed Co", connection.CompanyName);
        Assert.Equal(TestQbo.OtherUserId, connection.ConnectedBy);
        Assert.True(Assert.Single(connection.DomainEvents.OfType<QboConnectedDomainEvent>()).IsReconnect);
    }

    [Fact]
    public void Reconnect_revives_a_Disconnected_connection()
    {
        var connection = TestQbo.Connection();
        connection.Disconnect(TestBudgeting.ActorId, Now);

        var result = connection.Reconnect(
            TestQbo.RealmId, "Acme", QboEnvironment.Sandbox, "CAD", TestBudgeting.ActorId, Now.AddDays(100), Now);

        Assert.True(result.IsSuccess);
        Assert.True(connection.IsLive);
    }

    [Fact]
    public void Reconnect_still_requires_CAD()
    {
        var connection = TestQbo.Connection();

        var result = connection.Reconnect(
            TestQbo.RealmId, "Acme", QboEnvironment.Sandbox, "USD", TestBudgeting.ActorId, Now.AddDays(100), Now);

        Assert.Equal(QboConnectionErrors.HomeCurrencyNotCad, result.Error);
    }

    [Fact]
    public void MarkNeedsReconnect_raises_once_and_is_quiet_when_repeated()
    {
        var connection = TestQbo.Connection();
        connection.ClearDomainEvents();

        connection.MarkNeedsReconnect("invalid_grant", Now);
        connection.MarkNeedsReconnect("invalid_grant", Now.AddHours(6));

        Assert.Equal(QboConnectionStatus.NeedsReconnect, connection.Status);
        Assert.Equal("invalid_grant", connection.LastErrorCode);
        Assert.Single(connection.DomainEvents.OfType<QboConnectionNeedsReconnectDomainEvent>());
    }

    [Fact]
    public void A_disconnected_connection_cannot_be_flagged_or_disconnected_again()
    {
        var connection = TestQbo.Connection();
        connection.Disconnect(TestBudgeting.ActorId, Now);

        Assert.Equal(QboConnectionErrors.AlreadyDisconnected, connection.MarkNeedsReconnect("invalid_grant", Now).Error);
        Assert.Equal(QboConnectionErrors.AlreadyDisconnected, connection.Disconnect(TestBudgeting.ActorId, Now).Error);
        Assert.False(connection.IsLive);
    }

    [Fact]
    public void NoteRefreshTokenExpiry_records_only_a_move_of_a_day_or_more()
    {
        var connection = TestQbo.Connection();
        connection.ClearDomainEvents();

        Assert.False(connection.NoteRefreshTokenExpiry(Now.AddDays(100).AddHours(5), Now));
        Assert.Empty(connection.DomainEvents);

        Assert.True(connection.NoteRefreshTokenExpiry(Now.AddDays(101), Now));
        Assert.Equal(Now.AddDays(101), connection.RefreshTokenExpiresAtUtc);
        Assert.Single(connection.DomainEvents.OfType<QboRefreshTokenRenewedDomainEvent>());
    }

    [Fact]
    public void A_long_company_name_is_clipped_not_refused()
    {
        var result = QboConnection.Connect(
            TestBudgeting.TenantId, TestQbo.RealmId, new string('x', 400), QboEnvironment.Sandbox, "CAD",
            TestBudgeting.ActorId, Now.AddDays(100), Now);

        Assert.Equal(QboConnection.CompanyNameMaxLength, result.Value.CompanyName.Length);
    }

    [Fact]
    public void The_aggregate_has_no_token_shaped_member()
    {
        // ModuleDbContext snapshots every aggregate in full into aggregate_snapshots. A property
        // named like a token here would be copied into the audit trail on every save.
        var members = typeof(QboConnection).GetProperties()
            .Select(p => p.Name)
            .Where(name => name.Contains("Token", StringComparison.OrdinalIgnoreCase)
                           && !name.EndsWith("ExpiresAtUtc", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(members);
    }
}
