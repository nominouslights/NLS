using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for `dotnet ef` (migrations only — never used at runtime).
/// Hardcodes the local superuser connection string (same credentials the Aspire AppHost's
/// Postgres resource uses) because design-time commands run outside the host's configuration
/// pipeline; migrations at runtime use the host's connection string instead.
/// </summary>
public sealed class TripsDbContextFactory : IDesignTimeDbContextFactory<TripsDbContext>
{
    public TripsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TripsDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=northernlink;Username=northernlink;Password=northernlink_dev",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", TripsServiceCollectionExtensions.SchemaName))
            .Options;

        return new TripsDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantType? TenantType => null;
    }
}
