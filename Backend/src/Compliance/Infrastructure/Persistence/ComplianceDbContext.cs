using Microsoft.EntityFrameworkCore;
using NorthernLink.Compliance.Application;
using NorthernLink.Compliance.Application.Integration;
using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Insurance;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Compliance.Domain.Registrations;
using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Compliance.Domain.RuleSets;
using NorthernLink.Compliance.Infrastructure.Persistence.ReadModels;
using NorthernLink.Shared.Persistence;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Compliance.Infrastructure.Persistence;

/// <summary>
/// The Compliance module's DbContext (Postgres schema "compliance"). Tenant stamping, the audit
/// pipeline (event journal + aggregate snapshots + outbox) and aggregate conventions come from
/// <see cref="ModuleDbContext"/>; this class only maps Compliance's own entities and their query
/// filters. The database half of tenant enforcement (RLS) is enabled in the migrations and keyed
/// on the session variable set by <see cref="TenantSessionInterceptor"/>.
/// <para>
/// Two tables are <b>reference data with no tenant</b> — <c>rule_sets</c> and
/// <c>regulatory_basis</c>: the law is the same for every carrier, so they carry no query filter,
/// and their RLS admits every read and only system-session writes.
/// </para>
/// </summary>
public sealed class ComplianceDbContext(
    DbContextOptions<ComplianceDbContext> options,
    ITenantContext tenantContext,
    ComplianceIntegrationEventMapper? integrationEventMapper = null)
    : ModuleDbContext(options, ComplianceServiceCollectionExtensions.SchemaName, tenantContext, integrationEventMapper)
{
    public DbSet<ComplianceRecord> ComplianceRecords => Set<ComplianceRecord>();
    public DbSet<DocumentSeries> DocumentSeries => Set<DocumentSeries>();
    public DbSet<TenantComplianceSettings> TenantComplianceSettings => Set<TenantComplianceSettings>();
    public DbSet<TenantRuleSetApplication> TenantRuleSetApplications => Set<TenantRuleSetApplication>();
    public DbSet<DestructionLogEntry> DestructionLog => Set<DestructionLogEntry>();
    public DbSet<CarrierAuthority> CarrierAuthorities => Set<CarrierAuthority>();
    public DbSet<InsurancePolicy> InsurancePolicies => Set<InsurancePolicy>();
    public DbSet<VehicleRegistration> VehicleRegistrations => Set<VehicleRegistration>();

    // Replicas of other modules' change streams — plain keyed rows, tenant-scoped.
    public DbSet<VehicleLookup> VehicleLookups => Set<VehicleLookup>();
    public DbSet<DriverLookup> DriverLookups => Set<DriverLookup>();
    public DbSet<UserLookup> UserLookups => Set<UserLookup>();

    // Reference data — no tenant_id, no query filter.
    public DbSet<RuleSet> RuleSets => Set<RuleSet>();
    public DbSet<RegulatoryBasis> RegulatoryBasis => Set<RegulatoryBasis>();

    // Read-side projection — an ordinary rm_* table secured by the same native RLS policy.
    public DbSet<ComplianceRecordReadModel> ComplianceRecordReadModels => Set<ComplianceRecordReadModel>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ComplianceRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ComplianceRecordReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentSeriesConfiguration());
        modelBuilder.ApplyConfiguration(new TenantComplianceSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new TenantRuleSetApplicationConfiguration());
        modelBuilder.ApplyConfiguration(new DestructionLogEntryConfiguration());
        modelBuilder.ApplyConfiguration(new RuleSetConfiguration());
        modelBuilder.ApplyConfiguration(new RegulatoryBasisConfiguration());
        modelBuilder.ApplyConfiguration(new CarrierAuthorityConfiguration());
        modelBuilder.ApplyConfiguration(new InsurancePolicyConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleRegistrationConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleLookupConfiguration());
        modelBuilder.ApplyConfiguration(new DriverLookupConfiguration());
        modelBuilder.ApplyConfiguration(new UserLookupConfiguration());

        // Tenant isolation, API half. Never remove: RLS is the backstop, not the substitute.
        // The filter must reference this context instance's TenantId property (not a captured
        // value) so EF never caches one tenant's id into the compiled model.
        modelBuilder.Entity<ComplianceRecord>().HasQueryFilter(r => r.TenantId == TenantId);
        modelBuilder.Entity<DocumentSeries>().HasQueryFilter(s => s.TenantId == TenantId);
        modelBuilder.Entity<TenantComplianceSettings>().HasQueryFilter(s => s.TenantId == TenantId);
        modelBuilder.Entity<TenantRuleSetApplication>().HasQueryFilter(a => a.TenantId == TenantId);
        modelBuilder.Entity<DestructionLogEntry>().HasQueryFilter(d => d.TenantId == TenantId);
        modelBuilder.Entity<ComplianceRecordReadModel>().HasQueryFilter(r => r.TenantId == TenantId);
        modelBuilder.Entity<CarrierAuthority>().HasQueryFilter(a => a.TenantId == TenantId);
        modelBuilder.Entity<InsurancePolicy>().HasQueryFilter(p => p.TenantId == TenantId);
        modelBuilder.Entity<VehicleRegistration>().HasQueryFilter(r => r.TenantId == TenantId);

        // And on the replicas. The lookup repositories' upserts bypass these deliberately and
        // match on (key, tenant) explicitly — see LookupRepositories.
        modelBuilder.Entity<VehicleLookup>().HasQueryFilter(v => v.TenantId == TenantId);
        modelBuilder.Entity<DriverLookup>().HasQueryFilter(d => d.TenantId == TenantId);
        modelBuilder.Entity<UserLookup>().HasQueryFilter(u => u.TenantId == TenantId);
    }
}
