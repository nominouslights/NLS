using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Fleet.Domain.Vehicles;

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

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new VehicleConfiguration());
        modelBuilder.ApplyConfiguration(new RetirementCertificateConfiguration());

        // Tenant isolation, API half. Never remove: RLS is the backstop, not the substitute.
        modelBuilder.Entity<Vehicle>().HasQueryFilter(v => v.TenantId == TenantId);
        modelBuilder.Entity<RetirementCertificate>().HasQueryFilter(c => c.TenantId == TenantId);
    }
}
