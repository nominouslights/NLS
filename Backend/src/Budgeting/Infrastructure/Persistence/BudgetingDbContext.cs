using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Budgeting.Application;
using NorthernLink.Budgeting.Application.Integration;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Budgeting.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>
/// The Budgeting module's DbContext (Postgres schema "budgeting"). Tenant stamping,
/// the audit pipeline (event journal + aggregate snapshots + outbox), and aggregate
/// conventions all come from <see cref="ModuleDbContext"/>; this class only maps
/// Budgeting's own entities and their query filters. The database half of tenant
/// enforcement (RLS) is enabled in the migrations and keyed on the session variable set by
/// <see cref="TenantSessionInterceptor"/>.
/// </summary>
public sealed class BudgetingDbContext(
    DbContextOptions<BudgetingDbContext> options,
    ITenantContext tenantContext,
    BudgetingIntegrationEventMapper? integrationEventMapper = null)
    : ModuleDbContext(options, BudgetingServiceCollectionExtensions.SchemaName, tenantContext, integrationEventMapper)
{
    public DbSet<BudgetPeriod> BudgetPeriods => Set<BudgetPeriod>();
    public DbSet<BudgetCode> BudgetCodes => Set<BudgetCode>();

    // Read-side projections — ordinary rm_* tables the projection worker upserts into,
    // secured by the same native RLS policy as every other table.
    public DbSet<BudgetPeriodReadModel> BudgetPeriodReadModels => Set<BudgetPeriodReadModel>();
    public DbSet<BudgetCodeReadModel> BudgetCodeReadModels => Set<BudgetCodeReadModel>();

    // Replica of Identity's users, upserted from identity.user-changed. Not an aggregate and not
    // a read model — a plain keyed table this module owns but does not author.
    public DbSet<UserLookup> UserLookups => Set<UserLookup>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new BudgetPeriodConfiguration());
        modelBuilder.ApplyConfiguration(new BudgetPeriodReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new BudgetCodeConfiguration());
        modelBuilder.ApplyConfiguration(new BudgetCodeReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new UserLookupConfiguration());

        // Tenant isolation, API half. Never remove: RLS is the backstop, not the substitute.
        // The filter must reference this context instance's TenantId property (not a captured
        // value) so EF never caches one tenant's id into the compiled model.
        modelBuilder.Entity<BudgetPeriod>().HasQueryFilter(p => p.TenantId == TenantId);
        modelBuilder.Entity<BudgetCode>().HasQueryFilter(c => c.TenantId == TenantId);

        // Same tenant filter on the read models — the retained API half of dual enforcement.
        modelBuilder.Entity<BudgetPeriodReadModel>().HasQueryFilter(p => p.TenantId == TenantId);
        modelBuilder.Entity<BudgetCodeReadModel>().HasQueryFilter(c => c.TenantId == TenantId);

        // And on the replica. UserLookupRepository's upsert bypasses this deliberately — see the
        // reasoning in LookupRepositories.cs — but every read path goes through it.
        modelBuilder.Entity<UserLookup>().HasQueryFilter(u => u.TenantId == TenantId);
    }
}
