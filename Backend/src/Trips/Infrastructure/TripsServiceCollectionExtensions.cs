using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.IntegrationEvents.Billing;
using NorthernLink.Shared.IntegrationEvents.Clients;
using NorthernLink.Shared.IntegrationEvents.Drivers;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Shared.Kernel;
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
using NorthernLink.Trips.Application.Manifests.Update;
using NorthernLink.Trips.Application.Routes;
using NorthernLink.Trips.Application.Routes.Create;
using NorthernLink.Trips.Application.Routes.GetRoutes;
using NorthernLink.Trips.Application.Routes.Update;
using NorthernLink.Trips.Application.Schedules;
using NorthernLink.Trips.Application.Stops;
using NorthernLink.Trips.Application.Stops.Create;
using NorthernLink.Trips.Application.Stops.GetStops;
using NorthernLink.Trips.Application.Stops.SetActive;
using NorthernLink.Trips.Application.Stops.Update;
using NorthernLink.Trips.Application.Schedules.Create;
using NorthernLink.Trips.Application.Schedules.GetScheduleTemplates;
using NorthernLink.Trips.Application.Schedules.SetActive;
using NorthernLink.Trips.Application.Schedules.Update;
using NorthernLink.Trips.Application.Trips;
using NorthernLink.Trips.Application.Trips.Assign;
using NorthernLink.Trips.Application.Trips.AttachManifest;
using NorthernLink.Trips.Application.Trips.ChangeStatus;
using NorthernLink.Trips.Application.Trips.CloseWithoutBilling;
using NorthernLink.Trips.Application.Trips.FinishOperations;
using NorthernLink.Trips.Application.Trips.Create;
using NorthernLink.Trips.Application.Trips.CreateDeadheadReturn;
using NorthernLink.Trips.Application.Trips.GetActivity;
using NorthernLink.Trips.Application.Trips.GetTripById;
using NorthernLink.Trips.Application.Trips.GetTrips;
using NorthernLink.Trips.Application.Trips.MergeRoundTrip;
using NorthernLink.Trips.Application.Trips.RecordDemand;
using NorthernLink.Trips.Application.Trips.UnpairRoundTrip;
using NorthernLink.Trips.Application.Trips.Update;
using NorthernLink.Trips.Domain.Manifests.Events;
using NorthernLink.Trips.Infrastructure.Generation;
using NorthernLink.Trips.Infrastructure.Persistence;
using NorthernLink.Trips.Infrastructure.Persistence.Projections;

namespace NorthernLink.Trips.Infrastructure;

/// <summary>
/// DI entry point for the Trips domain library — the only thing the API gateway sees.
/// Registers the library DbContext (Postgres schema "trips"), persistence services,
/// every CQRS handler explicitly (the reflection-based Sender resolves handlers from
/// DI; no assembly scanning), the library's integration event consumers, the read-side
/// projections, and the trip generation worker.
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
                    RequiredEnvironmentVariable.Get("ConnectionStrings__Postgres"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName))
                .AddInterceptors(serviceProvider.GetRequiredService<TenantSessionInterceptor>()));

        // 2. Persistence + read services. The mapper is registered as its concrete type —
        //    every module has its own IIntegrationEventMapper, so the interface can't be
        //    a single DI registration. The outbox dispatcher drains trips.outbox_messages.
        services.AddScoped<TripsIntegrationEventMapper>();
        services.AddHostedService<OutboxDispatcher<TripsDbContext>>();
        services.AddScoped<ITripManifestRepository, TripManifestRepository>();
        services.AddScoped<ITripManifestReadService, TripManifestReadService>();
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<ITripReadService, TripReadService>();
        services.AddScoped<ITripActivityReadService, TripActivityReadService>();
        services.AddScoped<IRouteRepository, RouteRepository>();
        services.AddScoped<IRouteReadService, RouteReadService>();
        services.AddScoped<IStopRepository, StopRepository>();
        services.AddScoped<IStopReadService, StopReadService>();
        services.AddScoped<IScheduleTemplateRepository, ScheduleTemplateRepository>();
        services.AddScoped<IScheduleTemplateReadService, ScheduleTemplateReadService>();
        services.AddScoped<IDriverLookupRepository, DriverLookupRepository>();
        services.AddScoped<IVehicleLookupRepository, VehicleLookupRepository>();
        services.AddScoped<IClientLookupRepository, ClientLookupRepository>();
        services.AddScoped<ITripBillingRepository, TripBillingRepository>();
        services.AddScoped<ITripNumberGenerator, TripNumberGenerator>();

        // 3. Command/query handlers — registered explicitly, one line per handler.
        services.AddScoped<ICommandHandler<CreateTripManifestCommand, Guid>, CreateTripManifestCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateTripManifestCommand>, UpdateTripManifestCommandHandler>();
        services.AddScoped<IQueryHandler<GetTripManifestsQuery, IReadOnlyList<TripManifestResponse>>, GetTripManifestsQueryHandler>();
        services.AddScoped<IQueryHandler<GetTripManifestByIdQuery, TripManifestResponse>, GetTripManifestByIdQueryHandler>();
        services.AddScoped<ICommandHandler<CreateTripCommand, Guid>, CreateTripCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateTripCommand>, UpdateTripCommandHandler>();
        services.AddScoped<ICommandHandler<AssignTripCommand>, AssignTripCommandHandler>();
        services.AddScoped<ICommandHandler<ChangeTripStatusCommand>, ChangeTripStatusCommandHandler>();
        services.AddScoped<ICommandHandler<FinishTripOperationsCommand>, FinishTripOperationsCommandHandler>();
        services.AddScoped<ICommandHandler<CloseTripWithoutBillingCommand>, CloseTripWithoutBillingCommandHandler>();
        services.AddScoped<ICommandHandler<RecordTripDemandCommand>, RecordTripDemandCommandHandler>();
        services.AddScoped<ICommandHandler<AttachManifestToTripCommand>, AttachManifestToTripCommandHandler>();
        services.AddScoped<ICommandHandler<MergeRoundTripCommand>, MergeRoundTripCommandHandler>();
        services.AddScoped<ICommandHandler<UnpairRoundTripCommand>, UnpairRoundTripCommandHandler>();
        services.AddScoped<ICommandHandler<CreateDeadheadReturnCommand, Guid>, CreateDeadheadReturnCommandHandler>();
        services.AddScoped<IQueryHandler<GetTripsQuery, IReadOnlyList<TripResponse>>, GetTripsQueryHandler>();
        services.AddScoped<IQueryHandler<GetTripByIdQuery, TripResponse>, GetTripByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetTripActivityQuery, IReadOnlyList<TripActivityEntryResponse>>, GetTripActivityQueryHandler>();
        services.AddScoped<ICommandHandler<CreateRouteCommand, Guid>, CreateRouteCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateRouteCommand>, UpdateRouteCommandHandler>();
        services.AddScoped<IQueryHandler<GetRoutesQuery, IReadOnlyList<RouteResponse>>, GetRoutesQueryHandler>();
        services.AddScoped<ICommandHandler<CreateStopCommand, Guid>, CreateStopCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateStopCommand>, UpdateStopCommandHandler>();
        services.AddScoped<ICommandHandler<SetStopActiveCommand>, SetStopActiveCommandHandler>();
        services.AddScoped<IQueryHandler<GetStopsQuery, IReadOnlyList<StopResponse>>, GetStopsQueryHandler>();
        services.AddScoped<ICommandHandler<CreateScheduleTemplateCommand, Guid>, CreateScheduleTemplateCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateScheduleTemplateCommand>, UpdateScheduleTemplateCommandHandler>();
        services.AddScoped<ICommandHandler<SetScheduleTemplateActiveCommand>, SetScheduleTemplateActiveCommandHandler>();
        services.AddScoped<IQueryHandler<GetScheduleTemplatesQuery, IReadOnlyList<ScheduleTemplateResponse>>, GetScheduleTemplatesQueryHandler>();

        // 4. Integration event consumers — the Drivers/Fleet/Clients replicas that keep
        //    driver_lookup/vehicle_lookup/client_lookup current for assignment validation,
        //    plus the trip inspection flags. Storing/projecting events, so they arrive by
        //    polling the producer outboxes in-database, not via RabbitMQ.
        services.AddOutboxPollingConsumer<TripsDbContext>(SchemaName, subscriptions => subscriptions
            .On<DriverChangedIntegrationEvent, DriverChangedIntegrationEventHandler>()
            .On<VehicleChangedIntegrationEvent, VehicleChangedIntegrationEventHandler>()
            .On<VehicleInspectionRecordedIntegrationEvent, VehicleInspectionRecordedIntegrationEventHandler>()
            .On<VehicleInspectionRemovedIntegrationEvent, VehicleInspectionRemovedIntegrationEventHandler>()
            .On<ClientChangedIntegrationEvent, ClientChangedIntegrationEventHandler>()
            .On<InvoiceBillingStateChangedIntegrationEvent, InvoiceBillingStateChangedIntegrationEventHandler>());

        // 5. Read-side projections — one worker upserts trips.rm_* from the journal, and a
        //    newly created manifest triggers the idempotent link-to-trip reaction (same-module
        //    secondary command, the Fleet EnsureRetirementCertificate pattern).
        services.AddProjections<TripsDbContext>(SchemaName, registry => registry
            .Project(new TripManifestProjection())
            .Project(new TripProjection())
            .Project(new RouteProjection())
            .Project(new StopProjection())
            .Project(new ScheduleTemplateProjection())
            .OnEvent<TripManifestCreatedDomainEvent>(entry =>
                new AttachManifestToTripCommand(entry.AggregateId)));

        // 6. Trip generation — materializes upcoming trips from active schedule templates.
        var generationOptions = configuration.GetSection(TripGenerationOptions.SectionName).Get<TripGenerationOptions>()
            ?? new TripGenerationOptions();
        services.AddSingleton(generationOptions);
        services.AddHostedService<TripGenerationWorker>();

        return services;
    }

}
