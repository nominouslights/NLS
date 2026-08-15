using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for `dotnet ef` (migrations only — never used at runtime).
/// Design-time commands run outside the host's configuration pipeline, so this reads
/// ConnectionStrings__Postgres from the environment directly — same as the runtime
/// registration in BudgetingServiceCollectionExtensions — rather than hardcoding a
/// value. Export it in your shell before running `dotnet ef` (see CLAUDE.md).
/// </summary>
public sealed class BudgetingDbContextFactory : IDesignTimeDbContextFactory<BudgetingDbContext>
{
    public BudgetingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BudgetingDbContext>()
            .UseNpgsql(
                RequiredEnvironmentVariable.Get("ConnectionStrings__Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", BudgetingServiceCollectionExtensions.SchemaName))
            .Options;

        return new BudgetingDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantType? TenantType => null;
    }
}
