using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NorthernLink.Shared.Tenancy;

/// <summary>
/// Sets the Postgres session variable <c>app.tenant_id</c> whenever a connection opens,
/// so Row-Level Security policies (<c>USING (tenant_id = current_setting('app.tenant_id', true)::uuid)</c>)
/// see the current tenant. This is the database half of the platform's dual tenant
/// enforcement — the EF global query filter is the API half. Reusable by every module:
/// add it to the module DbContext via <c>options.AddInterceptors(...)</c>.
/// </summary>
public sealed class TenantSessionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (tenantContext.TenantId is { } tenantId)
        {
            using var command = CreateSetTenantCommand(connection, tenantId);
            command.ExecuteNonQuery();
        }
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is { } tenantId)
        {
            var command = CreateSetTenantCommand(connection, tenantId);
            await using (command.ConfigureAwait(false))
            {
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static DbCommand CreateSetTenantCommand(DbConnection connection, Guid tenantId)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.tenant_id', @tenant_id, false);";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant_id";
        parameter.Value = tenantId.ToString();
        command.Parameters.Add(parameter);

        return command;
    }
}
