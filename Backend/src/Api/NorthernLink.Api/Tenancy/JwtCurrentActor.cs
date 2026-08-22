using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Api.Tenancy;

/// <summary>
/// Real actor resolution from the authenticated request's JWT claims, mirroring
/// <see cref="JwtTenantContext"/>. Reads the <c>sub</c> and <c>email</c> claims that
/// <c>JwtAccessTokenIssuer</c> stamps onto every access token.
/// <para>
/// The claim names are the literal JWT ones because <c>Program.cs</c> sets
/// <c>MapInboundClaims = false</c> — with the default legacy mapping, <c>sub</c> would arrive
/// renamed to a ClaimTypes URI and this would silently resolve to null on every request.
/// </para>
/// <para>
/// Unlike <see cref="JwtTenantContext"/> there is no ambient fallback: a tenant can be pushed by
/// background work because the work is genuinely being done on that tenant's behalf, but nobody
/// is acting when a worker drains an outbox. Null is the correct and honest answer there.
/// </para>
/// </summary>
public sealed class JwtCurrentActor(IHttpContextAccessor httpContextAccessor) : ICurrentActor
{
    public Guid? UserId
    {
        get
        {
            var claim = Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(claim, out var userId) ? userId : null;
        }
    }

    public string? Email => Principal?.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

    private ClaimsPrincipal? Principal =>
        httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user : null;
}
