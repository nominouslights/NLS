using System.Net;
using System.Text;
using NorthernLink.Budgeting.Application.Qbo;
using NorthernLink.Budgeting.Domain.Qbo;
using NorthernLink.Budgeting.Infrastructure.Qbo;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// The Intuit OAuth client against a stub <see cref="HttpMessageHandler"/>: wire format (Basic
/// header, form body, JSON revoke), the <c>invalid_grant</c> mapping, and that no token or code
/// ever appears in an exception message.
/// </summary>
public class IntuitOAuthClientTests
{
    private const string ClientId = "ABclientid123";
    private const string ClientSecret = "s3cr3t-value";
    private const string SecretAccessToken = "eyJ-ACCESS-TOKEN-NEVER-LOG";
    private const string SecretRefreshToken = "AB11-REFRESH-TOKEN-NEVER-LOG";

    private static readonly QboOptions Options = new();

    private static IntuitOAuthClient Client(StubHttpHandler handler, bool configured = true) =>
        new(new HttpClient(handler), Options, new FakeClock(TestQbo.Now))
        {
            ReadSecret = name => configured
                ? name switch
                {
                    QboSecrets.ClientIdVariable => ClientId,
                    QboSecrets.ClientSecretVariable => ClientSecret,
                    _ => throw new InvalidOperationException($"The {name} environment variable is not set."),
                }
                : throw new InvalidOperationException($"The {name} environment variable is not set."),
        };

    private static StubHttpHandler TokenEndpoint(HttpStatusCode status, string json) =>
        new((_, _) => StubHttpHandler.Json(status, json));

    private const string TokenJson = $$"""
        {"token_type":"bearer","expires_in":3600,"refresh_token":"{{SecretRefreshToken}}",
         "x_refresh_token_expires_in":8726400,"access_token":"{{SecretAccessToken}}"}
        """;

    [Fact]
    public void The_authorize_url_asks_for_the_accounting_scope_only()
    {
        var url = Client(TokenEndpoint(HttpStatusCode.OK, TokenJson)).BuildAuthorizeUrl("state-123");

        Assert.StartsWith(Options.AuthorizeUrl + "?", url, StringComparison.Ordinal);
        Assert.Contains($"client_id={ClientId}", url, StringComparison.Ordinal);
        Assert.Contains("response_type=code", url, StringComparison.Ordinal);
        Assert.Contains("scope=com.intuit.quickbooks.accounting&", url, StringComparison.Ordinal);
        Assert.Contains($"redirect_uri={Uri.EscapeDataString(Options.RedirectUri)}", url, StringComparison.Ordinal);
        Assert.Contains("state=state-123", url, StringComparison.Ordinal);
        Assert.DoesNotContain("openid", url, StringComparison.Ordinal);
        Assert.DoesNotContain("payment", url, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_client_id_is_NotConfigured()
    {
        var exception = Assert.Throws<QboAuthException>(
            () => Client(TokenEndpoint(HttpStatusCode.OK, TokenJson), configured: false).BuildAuthorizeUrl("s"));

        Assert.Equal(QboAuthFailure.NotConfigured, exception.Failure);
    }

    [Fact]
    public async Task Code_exchange_posts_a_form_with_Basic_auth_and_parses_the_grant()
    {
        var handler = TokenEndpoint(HttpStatusCode.OK, TokenJson);

        var grant = await Client(handler).ExchangeCodeAsync("auth-code-xyz", CancellationToken.None);

        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(Options.TokenUrl, request.RequestUri!.ToString());
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}")),
            request.Headers.Authorization.Parameter);
        Assert.Equal("application/x-www-form-urlencoded", request.Content!.Headers.ContentType!.MediaType);
        Assert.Contains("grant_type=authorization_code", body, StringComparison.Ordinal);
        Assert.Contains("code=auth-code-xyz", body, StringComparison.Ordinal);
        Assert.Contains($"redirect_uri={Uri.EscapeDataString(Options.RedirectUri)}", body, StringComparison.Ordinal);

        Assert.Equal(SecretAccessToken, grant.AccessToken);
        Assert.Equal(SecretRefreshToken, grant.RefreshToken);
        Assert.Equal(TestQbo.Now.AddSeconds(3600), grant.AccessTokenExpiresAtUtc);
        Assert.Equal(TestQbo.Now.AddSeconds(8726400), grant.RefreshTokenExpiresAtUtc);
    }

    [Fact]
    public async Task Refresh_posts_the_refresh_grant()
    {
        var handler = TokenEndpoint(HttpStatusCode.OK, TokenJson);

        await Client(handler).RefreshAsync(SecretRefreshToken, CancellationToken.None);

        var body = Assert.Single(handler.Requests).Body;
        Assert.Contains("grant_type=refresh_token", body, StringComparison.Ordinal);
        Assert.Contains($"refresh_token={SecretRefreshToken}", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_grant_maps_to_InvalidGrant_and_the_message_carries_no_token()
    {
        var handler = TokenEndpoint(
            HttpStatusCode.BadRequest,
            $$"""{"error":"invalid_grant","error_description":"Token {{SecretRefreshToken}} is invalid"}""");

        var exception = await Assert.ThrowsAsync<QboAuthException>(
            () => Client(handler).RefreshAsync(SecretRefreshToken, CancellationToken.None));

        Assert.Equal(QboAuthFailure.InvalidGrant, exception.Failure);
        Assert.Contains("invalid_grant", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretRefreshToken, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Another_error_is_Rejected_and_an_odd_error_field_is_not_echoed()
    {
        var handler = TokenEndpoint(HttpStatusCode.Unauthorized, $$"""{"error":"{{SecretAccessToken}}"}""");

        var exception = await Assert.ThrowsAsync<QboAuthException>(
            () => Client(handler).ExchangeCodeAsync("auth-code-xyz", CancellationToken.None));

        Assert.Equal(QboAuthFailure.Rejected, exception.Failure);
        Assert.Contains("401", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretAccessToken, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("auth-code-xyz", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_body_that_is_not_a_token_response_is_Unreachable_without_echoing_it()
    {
        var handler = new StubHttpHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"<html>{SecretAccessToken}") });

        var exception = await Assert.ThrowsAsync<QboAuthException>(
            () => Client(handler).ExchangeCodeAsync("c", CancellationToken.None));

        Assert.Equal(QboAuthFailure.Unreachable, exception.Failure);
        Assert.DoesNotContain(SecretAccessToken, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_transport_failure_is_Unreachable()
    {
        var handler = new StubHttpHandler((_, _) => throw new HttpRequestException("connection refused"));

        var exception = await Assert.ThrowsAsync<QboAuthException>(
            () => Client(handler).ExchangeCodeAsync("c", CancellationToken.None));

        Assert.Equal(QboAuthFailure.Unreachable, exception.Failure);
    }

    [Fact]
    public async Task Revoke_posts_json_with_Basic_auth()
    {
        var handler = new StubHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));

        await Client(handler).RevokeAsync(SecretRefreshToken, CancellationToken.None);

        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(Options.RevokeUrl, request.RequestUri!.ToString());
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
        Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal($$"""{"token":"{{SecretRefreshToken}}"}""", body);
    }

    [Fact]
    public void The_grant_never_prints_its_tokens()
    {
        var grant = new QboTokenGrant(SecretAccessToken, SecretRefreshToken, TestQbo.Now, TestQbo.Now);

        Assert.DoesNotContain(SecretAccessToken, grant.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SecretRefreshToken, grant.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_api_base_url_follows_the_environment()
    {
        Assert.Equal("https://sandbox-quickbooks.api.intuit.com", new QboOptions().ApiBaseUrl);
        Assert.Equal(
            "https://quickbooks.api.intuit.com",
            new QboOptions { Environment = QboEnvironment.Production }.ApiBaseUrl);
    }
}

/// <summary>The accounting client: path, pinned minor version, bearer header, and failure mapping.</summary>
public class QboAccountingClientTests
{
    private static QboAccountingClient Client(StubHttpHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(new QboOptions().ApiBaseUrl) }, new QboOptions());

    [Fact]
    public async Task Company_info_is_read_with_the_bearer_token_and_pinned_minor_version()
    {
        var handler = new StubHttpHandler((_, _) => StubHttpHandler.Json(
            HttpStatusCode.OK, """{"CompanyInfo":{"CompanyName":"Northern Link","Country":"CA"}}"""));

        var info = await Client(handler).GetCompanyInfoAsync(TestQbo.RealmId, "bearer-abc", CancellationToken.None);

        var request = Assert.Single(handler.Requests).Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            $"https://sandbox-quickbooks.api.intuit.com/v3/company/{TestQbo.RealmId}/companyinfo/{TestQbo.RealmId}?minorversion=75",
            request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("bearer-abc", request.Headers.Authorization.Parameter);
        Assert.Equal("Northern Link", info.CompanyName);
    }

    [Fact]
    public async Task Home_currency_comes_from_preferences()
    {
        var handler = new StubHttpHandler((_, _) => StubHttpHandler.Json(
            HttpStatusCode.OK, """{"Preferences":{"CurrencyPrefs":{"MultiCurrencyEnabled":false,"HomeCurrency":{"value":"CAD"}}}}"""));

        Assert.Equal("CAD", await Client(handler).GetHomeCurrencyAsync(TestQbo.RealmId, "t", CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, QboApiFailure.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests, QboApiFailure.Throttled)]
    [InlineData(HttpStatusCode.InternalServerError, QboApiFailure.Rejected)]
    public async Task Failures_are_classified_and_never_carry_the_token(HttpStatusCode status, QboApiFailure expected)
    {
        var handler = new StubHttpHandler((_, _) => StubHttpHandler.Json(status, """{"Fault":{}}"""));

        var exception = await Assert.ThrowsAsync<QboApiException>(
            () => Client(handler).GetCompanyInfoAsync(TestQbo.RealmId, "bearer-secret", CancellationToken.None));

        Assert.Equal(expected, exception.Failure);
        Assert.DoesNotContain("bearer-secret", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_realm_id_that_is_not_digits_never_reaches_the_wire()
    {
        var handler = new StubHttpHandler((_, _) => StubHttpHandler.Json(HttpStatusCode.OK, "{}"));

        await Assert.ThrowsAsync<QboApiException>(
            () => Client(handler).GetCompanyInfoAsync("1/../2", "t", CancellationToken.None));

        Assert.Empty(handler.Requests);
    }
}
