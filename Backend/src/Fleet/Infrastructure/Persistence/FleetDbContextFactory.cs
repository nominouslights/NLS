using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for `dotnet ef` (migrations only — never used at runtime).
/// Hardcodes the local superuser connection string (same credentials the Aspire AppHost's
/// Postgres resource uses) because design-time commands run outside the host's configuration
/// pipeline; migrations at runtime use the host's connection string instead.
/// </summary>
public sealed class FleetDbContextFactory : IDesignTimeDbContextFactory<FleetDbContext>
{
    public FleetDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=northernlink;Username=northernlink;Password=northernlink_dev",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", FleetServiceCollectionExtensions.SchemaName))
            .Options;

        return new FleetDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantType? TenantType => null;
    }
}
