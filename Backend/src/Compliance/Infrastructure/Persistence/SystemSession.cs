using Microsoft.EntityFrameworkCore;

namespace NorthernLink.Compliance.Infrastructure.Persistence;

/// <summary>
/// Runs a unit of work on a pinned connection opted into the tables' <c>app.is_system</c> RLS
/// policy — the same mechanics the projection and outbox workers use. Needed by the two
/// reference-data repositories: <c>rule_sets</c> and <c>regulatory_basis</c> carry no tenant, so
/// their write policy admits only the system session (a request session, which carries
/// <c>app.tenant_id</c>, would be refused). The caller is already authorized at the API half
/// (ComplianceAccess); this is the database half agreeing that a publish is a platform act, not
/// a tenant's. The connection is closed afterwards and Npgsql's pool reset clears the variable.
/// </summary>
internal static class SystemSession
{
    public static async Task RunAsync(DbContext context, Func<Task> work, CancellationToken cancellationToken)
    {
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.is_system', 'true', false);", cancellationToken);
            await work();
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
