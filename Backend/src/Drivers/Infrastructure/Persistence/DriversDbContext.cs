using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Application.Drivers;
using NorthernLink.Drivers.Domain.Clearances;
using NorthernLink.Drivers.Domain.Credentials;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>
/// The Drivers module's DbContext (Postgres schema "drivers"). Tenant stamping, the audit
/// pipeline (event journal + aggregate snapshots + outbox), and aggregate conventions
/// all come from <see cref="ModuleDbContext"/>; this class only maps Drivers' own
/// entities and their query filters. The database half of tenant enforcement (RLS) is
/// enabled in the migrations and keyed on the session variable set by
/// <see cref="TenantSessionInterceptor"/>.
/// </summary>
public sealed class DriversDbContext(
    DbContextOptions<DriversDbContext> options,
    ITenantContext tenantContext,
    DriversIntegrationEventMapper? integrationEventMapper = null)
    : ModuleDbContext(options, DriversServiceCollectionExtensions.SchemaName, tenantContext, integrationEventMapper)
{
    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<DriverCredential> DriverCredentials => Set<DriverCredential>();

    public DbSet<DriverClearance> DriverClearances => Set<DriverClearance>();

    public DbSet<HosLogEntry> HosLogEntries => Set<HosLogEntry>();

    public DbSet<DutyDay> DutyDays => Set<DutyDay>();

    // The compliance aggregates beside the ledger.
    public DbSet<HosException> HosExceptions => Set<HosException>();

    public DbSet<DriverOutOfService> DriverOutOfServiceRecords => Set<DriverOutOfService>();

    public DbSet<EldMalfunctionReport> EldMalfunctionReports => Set<EldMalfunctionReport>();

    public DbSet<ComplianceFinding> ComplianceFindings => Set<ComplianceFinding>();

    // Read-side projections — ordinary rm_* tables the projection worker upserts into,
    // secured by the same native RLS policy as every other table.
    public DbSet<DriverReadModel> DriverReadModels => Set<DriverReadModel>();

    public DbSet<DriverCredentialReadModel> DriverCredentialReadModels => Set<DriverCredentialReadModel>();

    public DbSet<DriverClearanceReadModel> DriverClearanceReadModels => Set<DriverClearanceReadModel>();

    public DbSet<HosLogEntryReadModel> HosLogEntryReadModels => Set<HosLogEntryReadModel>();

    public DbSet<DutyDayReadModel> DutyDayReadModels => Set<DutyDayReadModel>();

    public DbSet<HosExceptionReadModel> HosExceptionReadModels => Set<HosExceptionReadModel>();

    public DbSet<DriverOutOfServiceReadModel> DriverOutOfServiceReadModels => Set<DriverOutOfServiceReadModel>();

    public DbSet<EldMalfunctionReportReadModel> EldMalfunctionReportReadModels => Set<EldMalfunctionReportReadModel>();

    public DbSet<ComplianceFindingReadModel> ComplianceFindingReadModels => Set<ComplianceFindingReadModel>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new DriverConfiguration());
        modelBuilder.ApplyConfiguration(new DriverCredentialConfiguration());
        modelBuilder.ApplyConfiguration(new DriverClearanceConfiguration());
        modelBuilder.ApplyConfiguration(new HosLogEntryConfiguration());
        modelBuilder.ApplyConfiguration(new DutyDayConfiguration());
        modelBuilder.ApplyConfiguration(new HosExceptionConfiguration());
        modelBuilder.ApplyConfiguration(new DriverOutOfServiceConfiguration());
        modelBuilder.ApplyConfiguration(new EldMalfunctionReportConfiguration());
        modelBuilder.ApplyConfiguration(new ComplianceFindingConfiguration());

        modelBuilder.ApplyConfiguration(new DriverReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new DriverCredentialReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new DriverClearanceReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new HosLogEntryReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new DutyDayReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new HosExceptionReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new DriverOutOfServiceReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new EldMalfunctionReportReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new ComplianceFindingReadModelConfiguration());

        // Tenant isolation, API half. Never remove: RLS is the backstop, not the substitute.
        modelBuilder.Entity<Driver>().HasQueryFilter(d => d.TenantId == TenantId);
        modelBuilder.Entity<DriverCredential>().HasQueryFilter(c => c.TenantId == TenantId);
        modelBuilder.Entity<DriverClearance>().HasQueryFilter(c => c.TenantId == TenantId);
        modelBuilder.Entity<HosLogEntry>().HasQueryFilter(e => e.TenantId == TenantId);
        modelBuilder.Entity<DutyDay>().HasQueryFilter(d => d.TenantId == TenantId);
        modelBuilder.Entity<HosException>().HasQueryFilter(e => e.TenantId == TenantId);
        modelBuilder.Entity<DriverOutOfService>().HasQueryFilter(r => r.TenantId == TenantId);
        modelBuilder.Entity<EldMalfunctionReport>().HasQueryFilter(r => r.TenantId == TenantId);
        modelBuilder.Entity<ComplianceFinding>().HasQueryFilter(f => f.TenantId == TenantId);

        // Same tenant filter on the read models — the retained API half of dual enforcement.
        modelBuilder.Entity<DriverReadModel>().HasQueryFilter(d => d.TenantId == TenantId);
        modelBuilder.Entity<DriverCredentialReadModel>().HasQueryFilter(c => c.TenantId == TenantId);
        modelBuilder.Entity<DriverClearanceReadModel>().HasQueryFilter(c => c.TenantId == TenantId);
        modelBuilder.Entity<HosLogEntryReadModel>().HasQueryFilter(e => e.TenantId == TenantId);
        modelBuilder.Entity<DutyDayReadModel>().HasQueryFilter(d => d.TenantId == TenantId);
        modelBuilder.Entity<HosExceptionReadModel>().HasQueryFilter(e => e.TenantId == TenantId);
        modelBuilder.Entity<DriverOutOfServiceReadModel>().HasQueryFilter(r => r.TenantId == TenantId);
        modelBuilder.Entity<EldMalfunctionReportReadModel>().HasQueryFilter(r => r.TenantId == TenantId);
        modelBuilder.Entity<ComplianceFindingReadModel>().HasQueryFilter(f => f.TenantId == TenantId);
    }
}
