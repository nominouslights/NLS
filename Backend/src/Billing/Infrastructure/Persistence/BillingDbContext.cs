using Microsoft.EntityFrameworkCore;
using NorthernLink.Billing.Application.Invoices;
using NorthernLink.Billing.Domain.BillableTrips;
using NorthernLink.Billing.Domain.Contracts;
using NorthernLink.Billing.Domain.Invoices;
using NorthernLink.Billing.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Billing.Infrastructure.Persistence;

/// <summary>
/// The Billing module's DbContext (Postgres schema "billing"). Tenant stamping, the audit
/// pipeline (event journal + aggregate snapshots + outbox), and aggregate conventions all
/// come from <see cref="ModuleDbContext"/>; this class only maps Billing's own entities
/// and their query filters. Alongside the Invoice aggregate it maps two cross-module
/// replicas — <c>contract_snapshots</c> and <c>billable_trips</c> — plain rows maintained
/// by integration-event consumers, not aggregates (no journal/snapshot/outbox rows). The
/// database half of tenant enforcement (RLS) is enabled in the migration and keyed on the
/// session variable set by <see cref="TenantSessionInterceptor"/>.
/// </summary>
public sealed class BillingDbContext(
    DbContextOptions<BillingDbContext> options,
    ITenantContext tenantContext,
    BillingIntegrationEventMapper? integrationEventMapper = null)
    : ModuleDbContext(options, BillingServiceCollectionExtensions.SchemaName, tenantContext, integrationEventMapper)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<ContractSnapshot> ContractSnapshots => Set<ContractSnapshot>();

    public DbSet<BillableTrip> BillableTrips => Set<BillableTrip>();

    public DbSet<InvoiceReadModel> InvoiceReadModels => Set<InvoiceReadModel>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new InvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new ContractSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new BillableTripConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceReadModelConfiguration());

        // Tenant isolation, API half. Never remove: RLS is the backstop, not the substitute.
        modelBuilder.Entity<Invoice>().HasQueryFilter(i => i.TenantId == TenantId);
        modelBuilder.Entity<ContractSnapshot>().HasQueryFilter(c => c.TenantId == TenantId);
        modelBuilder.Entity<BillableTrip>().HasQueryFilter(t => t.TenantId == TenantId);
        modelBuilder.Entity<InvoiceReadModel>().HasQueryFilter(i => i.TenantId == TenantId);
    }
}
