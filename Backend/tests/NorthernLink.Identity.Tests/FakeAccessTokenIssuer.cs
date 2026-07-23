using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Identity.Domain.Users;

namespace NorthernLink.Identity.Tests;

/// <summary>
/// Deterministic fake of the token issuer: opaque tokens are sequential and their "hash"
/// is a reversible prefix, so tests can assert stored-hash-vs-raw-token relationships
/// without real crypto.
/// </summary>
internal sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    private int _opaqueTokenCount;

    public IssuedAccessToken IssueAccessToken(User user) =>
        new($"access-token-{user.Id}", DateTimeOffset.UtcNow.AddMinutes(15));

    public IssuedOpaqueToken IssueOpaqueToken(TimeSpan lifetime)
    {
        var rawToken = $"opaque-{++_opaqueTokenCount}";
        return new IssuedOpaqueToken(rawToken, HashOpaqueToken(rawToken), DateTimeOffset.UtcNow.Add(lifetime));
    }

    public string HashOpaqueToken(string rawToken) => $"hashed:{rawToken}";
}
