using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Application.Manifests;
using NorthernLink.Trips.Application.Manifests.Create;
using NorthernLink.Trips.Application.Manifests.GetById;
using NorthernLink.Trips.Application.Manifests.GetManifests;
using NorthernLink.Trips.Infrastructure.DevSeed;
using NorthernLink.Trips.Infrastructure.Persistence;

namespace NorthernLink.Trips.Infrastructure;

/// <summary>
/// DI entry point for the Trips domain library — the only thing the API gateway sees.
/// Registers the library DbContext (Postgres schema "trips"), persistence services,
/// every CQRS handler explicitly (the reflection-based Sender resolves handlers from
/// DI; no assembly scanning), and the library's integration event consumer.
/// </summary>
public static class TripsServiceCollectionExtensions
{
    public const string SchemaName = "trips";

    public static IServiceCollection AddTrips(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. DbContext — module schema, tenant session interceptor (RLS session variable).
        //    TryAdd: the interceptor is shared platform plumbing other modules also register.
        services.TryAddScoped<TenantSessionInterceptor>();

        services.AddDbContext<TripsDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(
                    configuration.GetConnectionString("Postgres"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName))
                .AddInterceptors(serviceProvider.GetRequiredService<TenantSessionInterceptor>()));

        // 2. Persistence + read services. The mapper is registered as its concrete type —
        //    every module has its own IIntegrationEventMapper, so the interface can't be
        //    a single DI registration. The outbox dispatcher drains trips.outbox_messages.
        services.AddScoped<TripsIntegrationEventMapper>();
        services.AddHostedService<OutboxDispatcher<TripsDbContext>>();
        services.AddScoped<ITripManifestRepository, TripManifestRepository>();
        services.AddScoped<ITripManifestReadService, TripManifestReadService>();

        // 3. Command/query handlers — registered explicitly, one line per handler.
        services.AddScoped<ICommandHandler<CreateTripManifestCommand, Guid>, CreateTripManifestCommandHandler>();
        services.AddScoped<IQueryHandler<GetTripManifestsQuery, IReadOnlyList<TripManifestResponse>>, GetTripManifestsQueryHandler>();
        services.AddScoped<IQueryHandler<GetTripManifestByIdQuery, TripManifestResponse>, GetTripManifestByIdQueryHandler>();

        // 4. Integration event consumers.
        services.AddIntegrationEventConsumer(SchemaName, subscriptions => subscriptions
            .On<VehicleStatusChangedIntegrationEvent, VehicleStatusChangedIntegrationEventHandler>());

        // 5. Read-side projections — one worker refreshes trips.mv_trip_manifests. Trips has no
        //    same-module secondary command today (cross-module reactions stay integration events).
        services.AddProjections<TripsDbContext>(SchemaName, registry => registry
            .OnAggregate("trip-manifest", "mv_trip_manifests"));

        return services;
    }

    /// <summary>
    /// Applies pending Trips migrations and, unless <c>DevSeed:IncludeDemoData</c> is set to
    /// false, seeds the demo manifest for <paramref name="tenantId"/>. The API host calls
    /// this in Development only. <paramref name="tenantId"/> is passed explicitly rather than
    /// resolved via <see cref="ITenantContext"/> — that interface now reflects the caller's
    /// JWT, which doesn't exist yet at startup.
    /// </summary>
    public static async Task InitializeTripsDatabaseAsync(this IServiceProvider serviceProvider, Guid tenantId)
    {
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<TripsDbContext>();
        await context.Database.MigrateAsync();

        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (configuration.GetValue("DevSeed:IncludeDemoData", true))
        {
            await TripsDevSeeder.SeedAsync(context, tenantId);
        }
    }
}
