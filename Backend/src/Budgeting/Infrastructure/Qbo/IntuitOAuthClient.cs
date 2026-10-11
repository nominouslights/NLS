using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Application.Qbo;

namespace NorthernLink.Budgeting.Infrastructure.Qbo;

/// <summary>
/// <see cref="IQboAuthClient"/> over Intuit's OAuth 2.0 endpoints — raw JSON through a typed
/// <see cref="HttpClient"/>, no SDK (the <c>PostmarkEmailSender</c> pattern). The token and revoke
/// endpoints take HTTP Basic <c>base64(client_id:client_secret)</c>; the token endpoint takes a
/// form-urlencoded body, the revoke endpoint a JSON one.
/// <para>
/// <b>Nothing secret reaches an exception message or a log.</b> A failure is described by the
/// HTTP status and Intuit's short <c>error</c> code, and that code is kept only when it looks
/// like one (lowercase letters and underscores). Response bodies are never echoed: Intuit's
/// <c>error_description</c> is free text, and free text is how a token ends up in a log.
/// </para>
/// </summary>
public sealed partial class IntuitOAuthClient(HttpClient httpClient, QboOptions options, TimeProvider clock)
    : IQboAuthClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Reads a secret by environment-variable name. <see cref="RequiredEnvironmentVariable.Get"/>
    /// in production; tests substitute a dictionary rather than mutate the process environment.
    /// </summary>
    public Func<string, string> ReadSecret { get; init; } = RequiredEnvironmentVariable.Get;

    public string BuildAuthorizeUrl(string state)
    {
        var clientId = Secret(QboSecrets.ClientIdVariable);

        var query = string.Join('&',
            Pair("client_id", clientId),
            Pair("response_type", "code"),
            Pair("scope", QboOptions.Scope),
            Pair("redirect_uri", options.RedirectUri),
            Pair("state", state));

        return $"{options.AuthorizeUrl}?{query}";

        static string Pair(string key, string value) => $"{key}={Uri.EscapeDataString(value)}";
    }

    public Task<QboTokenGrant> ExchangeCodeAsync(string code, CancellationToken cancellationToken) =>
        RequestTokensAsync(
            [
                new("grant_type", "authorization_code"),
                new("code", code),
                new("redirect_uri", options.RedirectUri),
            ],
            "code exchange",
            cancellationToken);

    public Task<QboTokenGrant> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        RequestTokensAsync(
            [
                new("grant_type", "refresh_token"),
                new("refresh_token", refreshToken),
            ],
            "token refresh",
            cancellationToken);

    public async Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.RevokeUrl);
        request.Headers.Authorization = BasicAuthorization();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(new RevokeRequest(token), options: SerializerOptions);

        using var response = await SendAsync(request, "revoke", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await FailureAsync(response, "revoke", cancellationToken);
        }
    }

    private async Task<QboTokenGrant> RequestTokensAsync(
        IEnumerable<KeyValuePair<string, string>> form,
        string operation,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.TokenUrl);
        request.Headers.Authorization = BasicAuthorization();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new FormUrlEncodedContent(form);

        var requestedAt = clock.GetUtcNow();
        using var response = await SendAsync(request, operation, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await FailureAsync(response, operation, cancellationToken);
        }

        TokenResponse? body;
        try
        {
            body = await response.Content.ReadFromJsonAsync<TokenResponse>(SerializerOptions, cancellationToken);
        }
        catch (JsonException)
        {
            // Not chained: a JsonException message can quote the text it choked on.
            throw new QboAuthException(QboAuthFailure.Unreachable, $"Intuit {operation} returned a body that is not valid JSON.");
        }

        if (body is not { AccessToken.Length: > 0, RefreshToken.Length: > 0, ExpiresIn: > 0, RefreshTokenExpiresIn: > 0 })
        {
            throw new QboAuthException(QboAuthFailure.Unreachable, $"Intuit {operation} returned an incomplete token response.");
        }

        // Expiries are measured from when the request was sent, so they err early, never late.
        return new QboTokenGrant(
            body.AccessToken,
            body.RefreshToken,
            requestedAt.AddSeconds(body.ExpiresIn),
            requestedAt.AddSeconds(body.RefreshTokenExpiresIn));
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // Transport messages name hosts, not payloads — safe to chain.
            throw new QboAuthException(QboAuthFailure.Unreachable, $"Intuit {operation} could not be reached.", exception);
        }
    }

    private static async Task<QboAuthException> FailureAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        var errorCode = await ReadErrorCodeAsync(response, cancellationToken);

        var failure = errorCode == "invalid_grant" ? QboAuthFailure.InvalidGrant : QboAuthFailure.Rejected;
        var suffix = errorCode is null ? string.Empty : $" ({errorCode})";
        return new QboAuthException(failure, $"Intuit {operation} failed with HTTP {status}{suffix}.");
    }

    /// <summary>Intuit's <c>error</c> field, only if it has the shape of an OAuth error code.</summary>
    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(SerializerOptions, cancellationToken);
            return error?.Error is { } code && OAuthErrorCode().IsMatch(code) ? code : null;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    private AuthenticationHeaderValue BasicAuthorization()
    {
        var credentials = $"{Secret(QboSecrets.ClientIdVariable)}:{Secret(QboSecrets.ClientSecretVariable)}";
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
    }

    private string Secret(string name)
    {
        try
        {
            return ReadSecret(name);
        }
        catch (InvalidOperationException)
        {
            throw new QboAuthException(QboAuthFailure.NotConfigured, $"The {name} environment variable is not set.");
        }
    }

    [GeneratedRegex("^[a-z_]{1,64}$")]
    private static partial Regex OAuthErrorCode();

    private sealed record RevokeRequest([property: JsonPropertyName("token")] string Token);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] long ExpiresIn,
        [property: JsonPropertyName("x_refresh_token_expires_in")] long RefreshTokenExpiresIn);

    private sealed record ErrorResponse([property: JsonPropertyName("error")] string? Error);
}
