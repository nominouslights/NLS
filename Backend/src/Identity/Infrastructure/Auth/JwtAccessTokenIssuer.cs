using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Identity.Infrastructure.Auth;

/// <summary>
/// Issues the interim bespoke JWT access token (HMAC-SHA256, short-lived) plus opaque
/// high-entropy tokens hashed with SHA-256 for server-side storage. This is explicitly the
/// interim, swappable mechanism ahead of a future full OpenIddict server — not that server
/// itself. Claim names ("tenant_id", "tenant_type", "role") must match what
/// <c>Api/Tenancy/JwtTenantContext.cs</c> reads and what <c>Program.cs</c> configures as
/// <c>TokenValidationParameters.RoleClaimType</c>.
///
/// Deliberately using the short literal "role" rather than <see cref="ClaimTypes.Role"/>'s
/// long URI as the claim type, with Program.cs both pointing RoleClaimType at it and setting
/// <c>MapInboundClaims = false</c>. Both halves are required: the bearer handler's legacy
/// inbound mapping is ON by default and would rename "role" to the ClaimTypes.Role URI before
/// RoleClaimType is consulted, silently breaking <c>RequireRole</c> with a 403.
/// </summary>
public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    public const string TenantIdClaimType = "tenant_id";
    public const string TenantTypeClaimType = "tenant_type";
    public const string RoleClaimType = "role";

    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    private readonly SigningCredentials _signingCredentials;

    public JwtAccessTokenIssuer()
    {
        var signingKey = RequiredEnvironmentVariable.Get("Identity__JwtSigningKey");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public IssuedAccessToken IssueAccessToken(User user)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAtUtc = now.Add(AccessTokenLifetime);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            // Only the Internal tenant exists today (see SeedTenant) — hardcoding the type
            // is a deliberate simplification until a Tenant lookup backs this claim.
            new Claim(TenantIdClaimType, user.TenantId.ToString()),
            new Claim(TenantTypeClaimType, TenantType.Internal.ToString()),
            new Claim(RoleClaimType, user.Role),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: _signingCredentials);

        var rawToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new IssuedAccessToken(rawToken, expiresAtUtc);
    }

    public IssuedOpaqueToken IssueOpaqueToken(TimeSpan lifetime)
    {
        var rawToken = GenerateRawToken();
        return new IssuedOpaqueToken(rawToken, HashOpaqueToken(rawToken), DateTimeOffset.UtcNow.Add(lifetime));
    }

    public string HashOpaqueToken(string rawToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    /// <summary>32 random bytes (256 bits), base64url-encoded — high entropy, URL/JSON safe.</summary>
    private static string GenerateRawToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
}
