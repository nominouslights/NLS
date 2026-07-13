using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Vehicles.ChangeStatus;
using NorthernLink.Fleet.Application.Vehicles.Dispose;
using NorthernLink.Fleet.Application.Vehicles.GetRetirementCertificate;
using NorthernLink.Fleet.Application.Vehicles.GetVehicleById;
using NorthernLink.Fleet.Application.Vehicles.GetVehicles;
using NorthernLink.Fleet.Application.Vehicles.RecordOdometer;
using NorthernLink.Fleet.Application.Vehicles.Register;
using NorthernLink.Fleet.Application.Vehicles.Update;
using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Fleet.Infrastructure.DevSeed;
using NorthernLink.Fleet.Infrastructure.Persistence;

namespace NorthernLink.Fleet.Infrastructure;

/// <summary>
/// DI entry point for the Fleet domain library — the only thing the API gateway sees.
/// Registers the library DbContext (Postgres schema "fleet"), persistence services,
/// and every CQRS handler explicitly
/// (the reflection-based Sender resolves handlers from DI; no assembly scanning).
/// </summary>
public static class FleetServiceCollectionExtensions
{
    public const string SchemaName = "fleet";

    public static IServiceCollection AddFleet(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. DbContext — module schema, tenant session interceptor (RLS session variable).
        //    TryAdd: the interceptor is shared platform plumbing other modules also register.
        services.TryAddScoped<TenantSessionInterceptor>();

        services.AddDbContext<FleetDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(
                    configuration.GetConnectionString("Postgres"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName))
                .AddInterceptors(serviceProvider.GetRequiredService<TenantSessionInterceptor>()));

        // 2. Persistence + read services. The mapper is registered as its concrete type —
        //    every module has its own IIntegrationEventMapper, so the interface can't be
        //    a single DI registration. The outbox dispatcher drains fleet.outbox_messages.
        services.AddScoped<FleetIntegrationEventMapper>();
        services.AddHostedService<OutboxDispatcher<FleetDbContext>>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IVehicleReadService, VehicleReadService>();

        // 3. Command/query handlers — registered explicitly, one line per handler.
        services.AddScoped<ICommandHandler<RegisterVehicleCommand, Guid>, RegisterVehicleCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateVehicleCommand>, UpdateVehicleCommandHandler>();
        services.AddScoped<ICommandHandler<ChangeVehicleStatusCommand>, ChangeVehicleStatusCommandHandler>();
        services.AddScoped<ICommandHandler<RecordOdometerCommand>, RecordOdometerCommandHandler>();
        services.AddScoped<ICommandHandler<DisposeVehicleCommand>, DisposeVehicleCommandHandler>();
        services.AddScoped<IQueryHandler<GetVehiclesQuery, IReadOnlyList<VehicleResponse>>, GetVehiclesQueryHandler>();
        services.AddScoped<IQueryHandler<GetVehicleByIdQuery, VehicleResponse>, GetVehicleByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetRetirementCertificateQuery, RetirementCertificateResponse>, GetRetirementCertificateQueryHandler>();

        return services;
    }

    /// <summary>
    /// Applies pending Fleet migrations and (when a tenant is resolvable) seeds the
    /// development demo vehicles. The API host calls this in Development only.
    /// </summary>
    public static async Task InitializeFleetDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        await context.Database.MigrateAsync();

        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        if (tenantContext.TenantId is { } tenantId)
        {
            await FleetDevSeeder.SeedAsync(context, tenantId);
        }
    }
}
