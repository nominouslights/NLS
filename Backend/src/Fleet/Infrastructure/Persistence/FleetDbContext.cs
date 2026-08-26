using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Fleet.Domain.Documents;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Domain.Services;
using NorthernLink.Fleet.Domain.Shops;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Domain.WorkOrders;
using NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// The Fleet module's DbContext (Postgres schema "fleet"). Tenant stamping, the audit
/// pipeline (event journal + aggregate snapshots + outbox), and aggregate conventions
/// all come from <see cref="ModuleDbContext"/>; this class only maps Fleet's own
/// entities and their query filters. The database half of tenant enforcement (RLS) is
/// enabled in the migrations and keyed on the session variable set by
/// <see cref="TenantSessionInterceptor"/>.
/// </summary>
public sealed class FleetDbContext(
    DbContextOptions<FleetDbContext> options,
    ITenantContext tenantContext,
    FleetIntegrationEventMapper? integrationEventMapper = null)
    : ModuleDbContext(options, FleetServiceCollectionExtensions.SchemaName, tenantContext, integrationEventMapper)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<RetirementCertificate> RetirementCertificates => Set<RetirementCertificate>();

    public DbSet<VehicleInspection> VehicleInspections => Set<VehicleInspection>();

    public DbSet<Shop> Shops => Set<Shop>();

    public DbSet<VehicleDocument> VehicleDocuments => Set<VehicleDocument>();

    public DbSet<ServiceRecord> ServiceRecords => Set<ServiceRecord>();

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<MaintenancePlan> MaintenancePlans => Set<MaintenancePlan>();

    public DbSet<PlanAssignment> PlanAssignments => Set<PlanAssignment>();

    public DbSet<PmCompletion> PmCompletions => Set<PmCompletion>();

    // Read-side projections — ordinary rm_* tables the projection worker upserts into, secured
    // by the same native RLS policy as every other table (a matview can't carry one, which is
    // what the previous projector-role/wrapper-view design existed to work around).
    public DbSet<VehicleReadModel> VehicleReadModels => Set<VehicleReadModel>();

    public DbSet<RetirementCertificateReadModel> RetirementCertificateReadModels => Set<RetirementCertificateReadModel>();

    public DbSet<VehicleInspectionReadModel> VehicleInspectionReadModels => Set<VehicleInspectionReadModel>();

    public DbSet<ShopReadModel> ShopReadModels => Set<ShopReadModel>();

    public DbSet<VehicleDocumentReadModel> VehicleDocumentReadModels => Set<VehicleDocumentReadModel>();

    public DbSet<ServiceRecordReadModel> ServiceRecordReadModels => Set<ServiceRecordReadModel>();

    public DbSet<WorkOrderReadModel> WorkOrderReadModels => Set<WorkOrderReadModel>();

    public DbSet<MaintenancePlanReadModel> MaintenancePlanReadModels => Set<MaintenancePlanReadModel>();

    public DbSet<PlanAssignmentReadModel> PlanAssignmentReadModels => Set<PlanAssignmentReadModel>();

    public DbSet<PmCompletionReadModel> PmCompletionReadModels => Set<PmCompletionReadModel>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new VehicleConfiguration());
        modelBuilder.ApplyConfiguration(new RetirementCertificateConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleInspectionConfiguration());
        modelBuilder.ApplyConfiguration(new ShopConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new ServiceRecordConfiguration());
        modelBuilder.ApplyConfiguration(new WorkOrderConfiguration());
        modelBuilder.ApplyConfiguration(new MaintenancePlanConfiguration());
        modelBuilder.ApplyConfiguration(new PlanAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new PmCompletionConfiguration());

        modelBuilder.ApplyConfiguration(new VehicleReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new RetirementCertificateReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleInspectionReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new ShopReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleDocumentReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new ServiceRecordReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new WorkOrderReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new MaintenancePlanReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new PlanAssignmentReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new PmCompletionReadModelConfiguration());

        // Tenant isolation, API half. Never remove: RLS is the backstop, not the substitute.
        modelBuilder.Entity<Vehicle>().HasQueryFilter(v => v.TenantId == TenantId);
        modelBuilder.Entity<RetirementCertificate>().HasQueryFilter(c => c.TenantId == TenantId);
        modelBuilder.Entity<VehicleInspection>().HasQueryFilter(i => i.TenantId == TenantId);
        modelBuilder.Entity<Shop>().HasQueryFilter(s => s.TenantId == TenantId);
        modelBuilder.Entity<VehicleDocument>().HasQueryFilter(d => d.TenantId == TenantId);
        modelBuilder.Entity<ServiceRecord>().HasQueryFilter(s => s.TenantId == TenantId);
        modelBuilder.Entity<WorkOrder>().HasQueryFilter(w => w.TenantId == TenantId);
        modelBuilder.Entity<MaintenancePlan>().HasQueryFilter(p => p.TenantId == TenantId);
        modelBuilder.Entity<PlanAssignment>().HasQueryFilter(a => a.TenantId == TenantId);
        modelBuilder.Entity<PmCompletion>().HasQueryFilter(c => c.TenantId == TenantId);

        // Same tenant filter on the read models — the wrapper view already filters by tenant
        // in the database, this is the retained API half of the dual enforcement.
        modelBuilder.Entity<VehicleReadModel>().HasQueryFilter(v => v.TenantId == TenantId);
        modelBuilder.Entity<RetirementCertificateReadModel>().HasQueryFilter(c => c.TenantId == TenantId);
        modelBuilder.Entity<VehicleInspectionReadModel>().HasQueryFilter(i => i.TenantId == TenantId);
        modelBuilder.Entity<ShopReadModel>().HasQueryFilter(s => s.TenantId == TenantId);
        modelBuilder.Entity<VehicleDocumentReadModel>().HasQueryFilter(d => d.TenantId == TenantId);
        modelBuilder.Entity<ServiceRecordReadModel>().HasQueryFilter(s => s.TenantId == TenantId);
        modelBuilder.Entity<WorkOrderReadModel>().HasQueryFilter(w => w.TenantId == TenantId);
        modelBuilder.Entity<MaintenancePlanReadModel>().HasQueryFilter(p => p.TenantId == TenantId);
        modelBuilder.Entity<PlanAssignmentReadModel>().HasQueryFilter(a => a.TenantId == TenantId);
        modelBuilder.Entity<PmCompletionReadModel>().HasQueryFilter(c => c.TenantId == TenantId);
    }
}
