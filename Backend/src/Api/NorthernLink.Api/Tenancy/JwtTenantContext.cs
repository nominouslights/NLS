using System.Security.Claims;
using NorthernLink.Identity.Infrastructure.Auth;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Api.Tenancy;

/// <summary>
/// Real tenant resolution from the authenticated request's JWT claims — replaces
/// <c>DevHeaderTenantContext</c> now that Identity issues real tokens (that class's own doc
/// comment said it was temporary until exactly this moment). Reads the <c>tenant_id</c> and
/// <c>tenant_type</c> claims <see cref="JwtAccessTokenIssuer"/> stamps onto every access
/// token. Registered unconditionally (not dev-gated): this is the real mechanism, not a dev
/// hack, so it applies the same way in every environment.
/// </summary>
public sealed class JwtTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid? TenantId
    {
        get
        {
            var claim = Principal?.FindFirst(JwtAccessTokenIssuer.TenantIdClaimType)?.Value;
            if (Guid.TryParse(claim, out var tenantId))
            {
                return tenantId;
            }

            // No authenticated principal — fall back to any ambient tenant set by background
            // work (e.g. the projection worker dispatching a secondary command "under the
            // journal row's tenant"). Null outside both, as before.
            return AmbientTenant.Current;
        }
    }

    public TenantType? TenantType
    {
        get
        {
            var claim = Principal?.FindFirst(JwtAccessTokenIssuer.TenantTypeClaimType)?.Value;
            return Enum.TryParse<Shared.Kernel.TenantType>(claim, out var tenantType) ? tenantType : null;
        }
    }

    private ClaimsPrincipal? Principal =>
        httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user : null;
}
