using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Application.Manifests;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Schedules;
using NorthernLink.Trips.Domain.Stops;
using NorthernLink.Trips.Domain.Trips;
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
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<Stop> Stops => Set<Stop>();
    public DbSet<ScheduleTemplate> ScheduleTemplates => Set<ScheduleTemplate>();

    /// <summary>Replicas upserted from Drivers/Fleet/Clients integration events (never joined cross-module).</summary>
    public DbSet<DriverLookup> DriverLookups => Set<DriverLookup>();
    public DbSet<VehicleLookup> VehicleLookups => Set<VehicleLookup>();
    public DbSet<ClientLookup> ClientLookups => Set<ClientLookup>();

    /// <summary>Per-tenant "TR-####" sequence rows — only touched by <see cref="TripNumberGenerator"/>.</summary>
    public DbSet<TripNumberCounter> TripNumberCounters => Set<TripNumberCounter>();

    /// <summary>Read-side projection tables (worker-maintained rm_* rows).</summary>
    public DbSet<TripManifestReadModel> ManifestReadModels => Set<TripManifestReadModel>();
    public DbSet<TripReadModel> TripReadModels => Set<TripReadModel>();
    public DbSet<RouteReadModel> RouteReadModels => Set<RouteReadModel>();
    public DbSet<StopReadModel> StopReadModels => Set<StopReadModel>();
    public DbSet<ScheduleTemplateReadModel> ScheduleTemplateReadModels => Set<ScheduleTemplateReadModel>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TripManifestConfiguration());
        modelBuilder.ApplyConfiguration(new TripConfiguration());
        modelBuilder.ApplyConfiguration(new RouteConfiguration());
        modelBuilder.ApplyConfiguration(new StopConfiguration());
        modelBuilder.ApplyConfiguration(new ScheduleTemplateConfiguration());
        modelBuilder.ApplyConfiguration(new DriverLookupConfiguration());
        modelBuilder.ApplyConfiguration(new VehicleLookupConfiguration());
        modelBuilder.ApplyConfiguration(new ClientLookupConfiguration());
        modelBuilder.ApplyConfiguration(new TripNumberCounterConfiguration());
        modelBuilder.ApplyConfiguration(new TripManifestReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new TripReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new RouteReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new StopReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new ScheduleTemplateReadModelConfiguration());

        // Tenant isolation, API half. Never remove: RLS is the backstop, not the substitute.
        modelBuilder.Entity<TripManifest>().HasQueryFilter(m => m.TenantId == TenantId);
        modelBuilder.Entity<Trip>().HasQueryFilter(t => t.TenantId == TenantId);
        modelBuilder.Entity<Route>().HasQueryFilter(r => r.TenantId == TenantId);
        modelBuilder.Entity<Stop>().HasQueryFilter(s => s.TenantId == TenantId);
        modelBuilder.Entity<ScheduleTemplate>().HasQueryFilter(t => t.TenantId == TenantId);
        modelBuilder.Entity<DriverLookup>().HasQueryFilter(d => d.TenantId == TenantId);
        modelBuilder.Entity<VehicleLookup>().HasQueryFilter(v => v.TenantId == TenantId);
        modelBuilder.Entity<ClientLookup>().HasQueryFilter(c => c.TenantId == TenantId);
        modelBuilder.Entity<TripNumberCounter>().HasQueryFilter(c => c.TenantId == TenantId);
        modelBuilder.Entity<TripManifestReadModel>().HasQueryFilter(m => m.TenantId == TenantId);
        modelBuilder.Entity<TripReadModel>().HasQueryFilter(t => t.TenantId == TenantId);
        modelBuilder.Entity<RouteReadModel>().HasQueryFilter(r => r.TenantId == TenantId);
        modelBuilder.Entity<StopReadModel>().HasQueryFilter(s => s.TenantId == TenantId);
        modelBuilder.Entity<ScheduleTemplateReadModel>().HasQueryFilter(t => t.TenantId == TenantId);
    }
}
