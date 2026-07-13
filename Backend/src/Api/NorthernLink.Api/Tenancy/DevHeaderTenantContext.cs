using NorthernLink.Shared.Tenancy;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Api.Tenancy;

/// <summary>
/// TEMPORARY: replaced by token claims when Identity (OpenIddict) lands.
///
/// Development-only tenant resolution from the <c>X-Tenant-Id</c> request header,
/// defaulting to the seed tenant when the header is absent (including outside an HTTP
/// request, e.g. during startup migrate/seed). A present-but-malformed header yields a
/// null tenant, which endpoints turn into 401. This is a deliberate, temporary security
/// hole for local development — it must NEVER be registered outside Development.
/// </summary>
public sealed class DevHeaderTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public const string HeaderName = "X-Tenant-Id";

    /// <summary>The Internal (Northern Link) seed tenant used across dev tooling.</summary>
    public static readonly Guid DefaultDevTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Guid? TenantId
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return DefaultDevTenantId;
            }

            if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var values))
            {
                return DefaultDevTenantId;
            }

            return Guid.TryParse(values.ToString(), out var tenantId) ? tenantId : null;
        }
    }

    public TenantType? TenantType => Shared.Kernel.TenantType.Internal;
}
