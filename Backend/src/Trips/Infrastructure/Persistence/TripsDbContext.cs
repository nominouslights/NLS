using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Manifests;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// The Trips module's DbContext (Postgres schema "trips"). Tenant stamping, the audit
/// pipeline (event journal + aggregate snapshots + outbox), and aggregate conventions
/// all come from <see cref="ModuleDbContext"/>; this class only maps Trips' own
/// entities and their query filters. The database half of tenant enforcement (RLS) is
/// enabled in the migrations and keyed on the session variable set by
/// <see cref="TenantSessionInterceptor"/>.
/// </summary>
public sealed class TripsDbContext(
    DbContextOptions<TripsDbContext> options,
    ITenantContext tenantContext,
    TripsIntegrationEventMapper? integrationEventMapper = null)
    : ModuleDbContext(options, TripsServiceCollectionExtensions.SchemaName, tenantContext, integrationEventMapper)
{
    public DbSet<TripManifest> Manifests => Set<TripManifest>();

    /// <summary>Read-side projection over trips.v_trip_manifests (read-only; worker keeps mv fresh).</summary>
    public DbSet<TripManifestReadModel> ManifestReadModels => Set<TripManifestReadModel>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TripManifestConfiguration());
        modelBuilder.ApplyConfiguration(new TripManifestReadModelConfiguration());

        // Tenant isolation, API half. Never remove: RLS is the backstop, not the substitute.
        modelBuilder.Entity<TripManifest>().HasQueryFilter(m => m.TenantId == TenantId);
        modelBuilder.Entity<TripManifestReadModel>().HasQueryFilter(m => m.TenantId == TenantId);
    }
}
