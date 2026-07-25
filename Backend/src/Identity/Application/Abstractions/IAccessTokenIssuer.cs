using NorthernLink.Identity.Domain.Users;

namespace NorthernLink.Identity.Application.Abstractions;

/// <summary>
/// Issues the interim bespoke JWT access token plus opaque high-entropy tokens (used both
/// for refresh tokens and, with a longer nominal lifetime, admin bootstrap tokens — both are
/// "a random secret whose hash we store", so they share one implementation). Implemented in
/// Infrastructure (<c>Infrastructure/Auth/JwtAccessTokenIssuer.cs</c>) so the JWT library and
/// signing key never leak into Application.
/// </summary>
public interface IAccessTokenIssuer
{
    /// <summary>Signs a short-lived access token carrying the user's id/email/tenant/role claims.</summary>
    IssuedAccessToken IssueAccessToken(User user);

    /// <summary>Generates a new random opaque token and its hash, expiring after <paramref name="lifetime"/>.</summary>
    IssuedOpaqueToken IssueOpaqueToken(TimeSpan lifetime);

    /// <summary>Hashes a raw opaque token the same way it was hashed at issuance, for comparison against a stored hash.</summary>
    string HashOpaqueToken(string rawToken);
}

/// <summary>A freshly signed access token and when it expires.</summary>
public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAtUtc);

/// <summary>
/// A freshly generated opaque token: <see cref="RawToken"/> is returned to the caller once
/// and never persisted; only <see cref="TokenHash"/> is stored server-side.
/// </summary>
public sealed record IssuedOpaqueToken(string RawToken, string TokenHash, DateTimeOffset ExpiresAtUtc);
