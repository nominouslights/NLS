using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Identity.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for `dotnet ef` (migrations only — never used at runtime).
/// Hardcodes the local superuser connection string (same credentials the Aspire AppHost's
/// Postgres resource uses) because design-time commands run outside the host's configuration
/// pipeline; migrations at runtime use the host's connection string instead.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=northernlink;Username=northernlink;Password=northernlink_dev",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityServiceCollectionExtensions.SchemaName))
            .Options;

        return new IdentityDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantType? TenantType => null;
    }
}
