using NorthernLink.Shared.Kernel;

namespace NorthernLink.Shared.Tenancy;

/// <summary>
/// Ambient tenant of the current request/operation, resolved from the access token's
/// <c>tenant_id</c> / <c>tenant_type</c> claims once authentication exists. Used by
/// API-level authorization checks and to set the Postgres RLS session variable —
/// the platform's dual-enforcement rule.
/// </summary>
public interface ITenantContext
{
    /// <summary>Null when the caller is unauthenticated or a non-tenant consumer.</summary>
    Guid? TenantId { get; }

    TenantType? TenantType { get; }
}
