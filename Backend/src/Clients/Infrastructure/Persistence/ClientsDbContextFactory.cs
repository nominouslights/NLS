using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for `dotnet ef` (migrations only — never used at runtime).
/// Design-time commands run outside the host's configuration pipeline, so this reads
/// ConnectionStrings__Postgres from the environment directly — same as the runtime
/// registration in ClientsServiceCollectionExtensions — rather than hardcoding a value.
/// Export it in your shell before running `dotnet ef` (see CLAUDE.md).
/// </summary>
public sealed class ClientsDbContextFactory : IDesignTimeDbContextFactory<ClientsDbContext>
{
    public ClientsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ClientsDbContext>()
            .UseNpgsql(
                RequiredEnvironmentVariable.Get("ConnectionStrings__Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ClientsServiceCollectionExtensions.SchemaName))
            .Options;

        return new ClientsDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantType? TenantType => null;
    }
}
