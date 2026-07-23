using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Clearances;
using NorthernLink.Drivers.Application.Clearances.GetForDriver;
using NorthernLink.Drivers.Application.Clearances.Grant;
using NorthernLink.Drivers.Application.Clearances.Revoke;
using NorthernLink.Drivers.Application.Credentials;
using NorthernLink.Drivers.Application.Credentials.Add;
using NorthernLink.Drivers.Application.Credentials.GetForDriver;
using NorthernLink.Drivers.Application.Credentials.Remove;
using NorthernLink.Drivers.Application.Credentials.SetImage;
using NorthernLink.Drivers.Application.Drivers;
using NorthernLink.Drivers.Application.Drivers.ChangeStatus;
using NorthernLink.Drivers.Application.Drivers.GetDriverById;
using NorthernLink.Drivers.Application.Drivers.GetDrivers;
using NorthernLink.Drivers.Application.Drivers.Register;
using NorthernLink.Drivers.Application.Drivers.Update;
using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Application.Hos.GetForDriver;
using NorthernLink.Drivers.Application.Hos.Record;
using NorthernLink.Drivers.Infrastructure.Persistence;
using NorthernLink.Drivers.Infrastructure.Persistence.Projections;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Infrastructure;

/// <summary>
/// DI entry point for the Drivers domain library — the only thing the API gateway sees.
/// Registers the library DbContext (Postgres schema "drivers"), persistence services,
/// and every CQRS handler explicitly
/// (the reflection-based Sender resolves handlers from DI; no assembly scanning).
/// </summary>
public static class DriversServiceCollectionExtensions
{
    public const string SchemaName = "drivers";

    public static IServiceCollection AddDrivers(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. DbContext — module schema, tenant session interceptor (RLS session variable).
        //    TryAdd: the interceptor is shared platform plumbing other modules also register.
        services.TryAddScoped<TenantSessionInterceptor>();

        services.AddDbContext<DriversDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(
                    RequiredEnvironmentVariable.Get("ConnectionStrings__Postgres"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName))
                .AddInterceptors(serviceProvider.GetRequiredService<TenantSessionInterceptor>()));

        // 2. Persistence + read services. The mapper is registered as its concrete type —
        //    every module has its own IIntegrationEventMapper, so the interface can't be
        //    a single DI registration. The outbox dispatcher drains drivers.outbox_messages.
        services.AddScoped<DriversIntegrationEventMapper>();
        services.AddHostedService<OutboxDispatcher<DriversDbContext>>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<IDriverReadService, DriverReadService>();
        services.AddScoped<IDriverCredentialRepository, DriverCredentialRepository>();
        services.AddScoped<IDriverCredentialReadService, DriverCredentialReadService>();
        services.AddScoped<IDriverClearanceRepository, DriverClearanceRepository>();
        services.AddScoped<IDriverClearanceReadService, DriverClearanceReadService>();
        services.AddScoped<IHosLogRepository, HosLogRepository>();
        services.AddScoped<IHosLogReadService, HosLogReadService>();

        // 3. Command/query handlers — registered explicitly, one line per handler.
        services.AddScoped<ICommandHandler<RegisterDriverCommand, Guid>, RegisterDriverCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateDriverCommand>, UpdateDriverCommandHandler>();
        services.AddScoped<ICommandHandler<ChangeDriverStatusCommand>, ChangeDriverStatusCommandHandler>();
        services.AddScoped<IQueryHandler<GetDriversQuery, IReadOnlyList<DriverResponse>>, GetDriversQueryHandler>();
        services.AddScoped<IQueryHandler<GetDriverByIdQuery, DriverResponse>, GetDriverByIdQueryHandler>();
        services.AddScoped<ICommandHandler<AddDriverCredentialCommand, Guid>, AddDriverCredentialCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveDriverCredentialCommand>, RemoveDriverCredentialCommandHandler>();
        services.AddScoped<ICommandHandler<SetDriverCredentialImageCommand>, SetDriverCredentialImageCommandHandler>();
        services.AddScoped<IQueryHandler<GetDriverCredentialsQuery, IReadOnlyList<DriverCredentialResponse>>, GetDriverCredentialsQueryHandler>();
        services.AddScoped<ICommandHandler<GrantDriverClearanceCommand, Guid>, GrantDriverClearanceCommandHandler>();
        services.AddScoped<ICommandHandler<RevokeDriverClearanceCommand>, RevokeDriverClearanceCommandHandler>();
        services.AddScoped<IQueryHandler<GetDriverClearancesQuery, IReadOnlyList<DriverClearanceResponse>>, GetDriverClearancesQueryHandler>();
        services.AddScoped<ICommandHandler<RecordHosEntryCommand, Guid>, RecordHosEntryCommandHandler>();
        services.AddScoped<IQueryHandler<GetHosEntriesQuery, IReadOnlyList<HosEntryResponse>>, GetHosEntriesQueryHandler>();

        // 4. No integration event consumers — Drivers only publishes (drivers.driver-changed);
        //    nothing upstream feeds it yet.

        // 5. Read-side projections — one worker polls drivers.event_journal and upserts the
        //    read-model rows for the aggregates the batch touched. Credential changes also
        //    refresh the parent driver row's denormalized credential fields.
        services.AddProjections<DriversDbContext>(SchemaName, registry => registry
            .Project(new DriverProjection())
            .Project(new DriverCredentialProjection())
            .Project(new DriverClearanceProjection())
            .Project(new HosLogEntryProjection()));

        return services;
    }
}
