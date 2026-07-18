using Microsoft.EntityFrameworkCore;

namespace NorthernLink.Identity.Infrastructure.Persistence;

/// <summary>
/// Opts the current connection into the <c>app.is_system</c> RLS escape hatch — the same
/// mechanism <c>OutboxDispatcher</c> uses for its tenant-less background access. Identity's
/// login, refresh, and admin-bootstrap flows are the request-path equivalent: they run
/// before any access token exists, so there is no ambient tenant to set
/// <c>app.tenant_id</c> from, yet they still need to read/write rows in tables RLS protects
/// (identity.users, identity.admin_bootstrap_tokens). Opening the connection explicitly here
/// means it stays open (and thus keeps this session variable) for the rest of the unit of
/// work, so a write later in the same request (e.g. bootstrap creating the new user) is
/// covered too — see the migration's <c>*_system_or_tenant</c> policies.
/// </summary>
internal static class SystemAccess
{
    public static async Task EnableAsync(DbContext context, CancellationToken cancellationToken)
    {
        await context.Database.OpenConnectionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.is_system', 'true', false);", cancellationToken);
    }
}
