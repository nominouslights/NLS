using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Compliance.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for `dotnet ef` (migrations only — never used at runtime).
/// Design-time commands run outside the host's configuration pipeline, so this reads
/// ConnectionStrings__Postgres from the environment directly — same as the runtime
/// registration in ComplianceServiceCollectionExtensions — rather than hardcoding a
/// value. Export it in your shell before running `dotnet ef` (see CLAUDE.md).
/// </summary>
public sealed class ComplianceDbContextFactory : IDesignTimeDbContextFactory<ComplianceDbContext>
{
    public ComplianceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseNpgsql(
                RequiredEnvironmentVariable.Get("ConnectionStrings__Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ComplianceServiceCollectionExtensions.SchemaName))
            .Options;

        return new ComplianceDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantType? TenantType => null;
    }
}
