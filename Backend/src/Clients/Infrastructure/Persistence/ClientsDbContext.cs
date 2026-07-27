using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Clients.Application.Clients;
using NorthernLink.Clients.Domain.Clients;
using NorthernLink.Clients.Domain.Contracts;
using NorthernLink.Clients.Domain.ClientContacts;
using NorthernLink.Clients.Domain.ClientInteractions;
using NorthernLink.Clients.Domain.PurchaseOrders;
using NorthernLink.Clients.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Clients.Infrastructure.Persistence;

/// <summary>
/// The Clients module's DbContext (Postgres schema "clients"). Tenant stamping, the audit
/// pipeline (event journal + aggregate snapshots + outbox), and aggregate conventions all
/// come from <see cref="ModuleDbContext"/>; this class only maps Clients' own entities and
/// their query filters. The database half of tenant enforcement (RLS) is enabled in the
/// migrations and keyed on the session variable set by <see cref="TenantSessionInterceptor"/>.
/// </summary>
public sealed class ClientsDbContext(
    DbContextOptions<ClientsDbContext> options,
    ITenantContext tenantContext,
    ClientsIntegrationEventMapper? integrationEventMapper = null)
    : ModuleDbContext(options, ClientsServiceCollectionExtensions.SchemaName, tenantContext, integrationEventMapper)
{
    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Contract> Contracts => Set<Contract>();

    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<ClientInteraction> ClientInteractions => Set<ClientInteraction>();

    // Read-side projections — ordinary rm_* tables the projection worker upserts into,
    // secured by the same native RLS policy as every other table.
    public DbSet<ClientReadModel> ClientReadModels => Set<ClientReadModel>();

    public DbSet<ContractReadModel> ContractReadModels => Set<ContractReadModel>();

    public DbSet<ClientContactReadModel> ClientContactReadModels => Set<ClientContactReadModel>();

    public DbSet<PurchaseOrderReadModel> PurchaseOrderReadModels => Set<PurchaseOrderReadModel>();

    public DbSet<ClientInteractionReadModel> ClientInteractionReadModels => Set<ClientInteractionReadModel>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ClientConfiguration());
        modelBuilder.ApplyConfiguration(new ContractConfiguration());
        modelBuilder.ApplyConfiguration(new ClientContactConfiguration());
        modelBuilder.ApplyConfiguration(new PurchaseOrderConfiguration());
        modelBuilder.ApplyConfiguration(new ClientInteractionConfiguration());

        modelBuilder.ApplyConfiguration(new ClientReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new ContractReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new ClientContactReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new PurchaseOrderReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new ClientInteractionReadModelConfiguration());

        // Tenant isolation, API half. Never remove: RLS is the backstop, not the substitute.
        modelBuilder.Entity<Client>().HasQueryFilter(c => c.TenantId == TenantId);
        modelBuilder.Entity<Contract>().HasQueryFilter(c => c.TenantId == TenantId);
        modelBuilder.Entity<ClientContact>().HasQueryFilter(c => c.TenantId == TenantId);
        modelBuilder.Entity<PurchaseOrder>().HasQueryFilter(p => p.TenantId == TenantId);
        modelBuilder.Entity<ClientInteraction>().HasQueryFilter(i => i.TenantId == TenantId);

        // Same tenant filter on the read models — the retained API half of dual enforcement.
        modelBuilder.Entity<ClientReadModel>().HasQueryFilter(c => c.TenantId == TenantId);
        modelBuilder.Entity<ContractReadModel>().HasQueryFilter(c => c.TenantId == TenantId);
        modelBuilder.Entity<ClientContactReadModel>().HasQueryFilter(c => c.TenantId == TenantId);
        modelBuilder.Entity<PurchaseOrderReadModel>().HasQueryFilter(p => p.TenantId == TenantId);
        modelBuilder.Entity<ClientInteractionReadModel>().HasQueryFilter(i => i.TenantId == TenantId);
    }
}
