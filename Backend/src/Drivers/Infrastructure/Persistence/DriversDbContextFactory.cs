using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for `dotnet ef` (migrations only — never used at runtime).
/// Design-time commands run outside the host's configuration pipeline, so this reads
/// ConnectionStrings__Postgres from the environment directly — same as the runtime
/// registration in DriversServiceCollectionExtensions — rather than hardcoding a value.
/// Export it in your shell before running `dotnet ef` (see CLAUDE.md).
/// </summary>
public sealed class DriversDbContextFactory : IDesignTimeDbContextFactory<DriversDbContext>
{
    public DriversDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DriversDbContext>()
            .UseNpgsql(
                RequiredEnvironmentVariable.Get("ConnectionStrings__Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", DriversServiceCollectionExtensions.SchemaName))
            .Options;

        return new DriversDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantType? TenantType => null;
    }
}
