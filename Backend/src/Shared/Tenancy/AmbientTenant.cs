namespace NorthernLink.Shared.Tenancy;

/// <summary>
/// Ambient tenant for work that runs outside an HTTP request but must still act as a
/// specific tenant — today, the projection worker dispatching a same-module secondary
/// command "under the tenant from the journal row". A background caller wraps the unit of
/// work in <see cref="Push"/>; the request-path <c>ITenantContext</c> consults
/// <see cref="Current"/> as a fallback when there is no authenticated principal, so the
/// existing <see cref="TenantSessionInterceptor"/> sets <c>app.tenant_id</c> for that work
/// exactly as it would for a real request. HTTP requests never touch this — the JWT claim
/// always wins there.
/// </summary>
public static class AmbientTenant
{
    private static readonly AsyncLocal<Guid?> Value = new();

    /// <summary>The tenant the current async flow is impersonating, or null.</summary>
    public static Guid? Current => Value.Value;

    /// <summary>Sets the ambient tenant until the returned scope is disposed (restores the prior value).</summary>
    public static IDisposable Push(Guid tenantId)
    {
        var previous = Value.Value;
        Value.Value = tenantId;
        return new Scope(previous);
    }

    private sealed class Scope(Guid? previous) : IDisposable
    {
        public void Dispose() => Value.Value = previous;
    }
}
