using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Infrastructure.Motive;

/// <summary>
/// <see cref="IMotiveCredentialProvider"/> over Motive's static API key: sets
/// <c>X-API-Key: &lt;key&gt;</c>. The key comes from the <c>Motive__ApiKey</c> environment variable
/// through <see cref="RequiredEnvironmentVariable"/> — never <c>IConfiguration</c> or
/// appsettings, so it can never land in a committed file — and is read <b>lazily, on every
/// call</b>, never at construction or registration. That is the Postmark / ObjectStorage
/// pattern: the API boots without the key, the first Motive request fails loudly with the
/// variable's name, and a rotated key takes effect without a restart.
/// </summary>
public sealed class ApiKeyMotiveCredentialProvider : IMotiveCredentialProvider
{
    /// <summary>The environment variable carrying the Motive API key.</summary>
    public const string ApiKeyVariable = "Motive__ApiKey";

    /// <summary>The header Motive reads the API key from.</summary>
    public const string HeaderName = "X-API-Key";

    /// <summary>
    /// Reads a secret by environment-variable name. <see cref="RequiredEnvironmentVariable.Get"/>
    /// in production; tests substitute a fake so they never mutate the process environment.
    /// </summary>
    public Func<string, string> ReadSecret { get; init; } = RequiredEnvironmentVariable.Get;

    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Read and validate before touching the request, so a missing or malformed key leaves it
        // unmodified.
        var apiKey = Sanitize(ReadSecret(ApiKeyVariable));

        request.Headers.Remove(HeaderName);

        // Validated add (never TryAddWithoutValidation): the header parser is a second line of
        // defence against a value that could split or smuggle headers.
        request.Headers.Add(HeaderName, apiKey);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Trims the key (a trailing newline from a pasted secret or an env file is the usual
    /// culprit) and refuses anything that is empty, or still carries whitespace or a control
    /// character, with the same loud configuration error a missing key gets. The message names
    /// the variable and never includes the value.
    /// </summary>
    private static string Sanitize(string? raw)
    {
        var key = raw?.Trim() ?? string.Empty;
        if (key.Length == 0 || key.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            throw new InvalidOperationException(
                $"The {ApiKeyVariable} environment variable is malformed: it is empty or contains whitespace or control characters.");
        }

        return key;
    }
}
