namespace NorthernLink.Drivers.Infrastructure.Motive;

/// <summary>
/// Applies Motive authentication to an outgoing request. This is the ONLY place a Motive
/// credential touches a request: the future Motive HTTP client builds a
/// <see cref="HttpRequestMessage"/> against <c>https://api.gomotive.com</c> and hands it here
/// before sending, never adding an auth header itself.
/// <para>
/// That is what lets authentication change without changing any caller. Today it is a static API
/// key (<see cref="ApiKeyMotiveCredentialProvider"/>, header <c>X-API-Key</c>); OAuth 2 later
/// becomes a second implementation that obtains and refreshes a token and sets
/// <c>Authorization: Bearer &lt;token&gt;</c> — which is why this is asynchronous even though the
/// API-key implementation completes synchronously.
/// </para>
/// <para>
/// Implementations read their secret lazily, per call, and throw
/// <see cref="InvalidOperationException"/> naming the missing environment variable when it is not
/// set — the API must still boot with no Motive configuration at all. They never log the secret.
/// </para>
/// </summary>
public interface IMotiveCredentialProvider
{
    /// <summary>Adds the credential to <paramref name="request"/>, replacing any already present.</summary>
    ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
