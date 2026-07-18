using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Documents;
using NorthernLink.Fleet.Application.Documents.Add;
using NorthernLink.Fleet.Application.Documents.GetAll;
using NorthernLink.Fleet.Application.Documents.GetForVehicle;
using NorthernLink.Fleet.Application.Documents.Remove;
using NorthernLink.Fleet.Application.Inspections;
using NorthernLink.Fleet.Application.Inspections.Enter;
using NorthernLink.Fleet.Application.Inspections.GetInspections;
using NorthernLink.Fleet.Application.Services;
using NorthernLink.Fleet.Application.Services.Add;
using NorthernLink.Fleet.Application.Services.GetForVehicle;
using NorthernLink.Fleet.Application.Shops;
using NorthernLink.Fleet.Application.Shops.GetShops;
using NorthernLink.Fleet.Application.Shops.Register;
using NorthernLink.Fleet.Application.Shops.Update;
using NorthernLink.Fleet.Application.Vehicles.ChangeStatus;
using NorthernLink.Fleet.Application.Vehicles.Dispose;
using NorthernLink.Fleet.Application.Vehicles.EnsureRetirementCertificate;
using NorthernLink.Fleet.Application.Vehicles.GetRetirementCertificate;
using NorthernLink.Fleet.Application.Vehicles.GetVehicleById;
using NorthernLink.Fleet.Application.Vehicles.GetVehicles;
using NorthernLink.Fleet.Application.Vehicles.RecordOdometer;
using NorthernLink.Fleet.Application.Vehicles.Register;
using NorthernLink.Fleet.Application.Vehicles.Update;
using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Fleet.Application.WorkOrders;
using NorthernLink.Fleet.Application.WorkOrders.ChangeStatus;
using NorthernLink.Fleet.Application.WorkOrders.Complete;
using NorthernLink.Fleet.Application.WorkOrders.Create;
using NorthernLink.Fleet.Application.WorkOrders.GetAll;
using NorthernLink.Fleet.Application.WorkOrders.GetForVehicle;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Fleet.Domain.Vehicles.Events;
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
        services.AddScoped<IVehicleInspectionRepository, VehicleInspectionRepository>();
        services.AddScoped<IVehicleInspectionReadService, VehicleInspectionReadService>();
        services.AddScoped<IShopRepository, ShopRepository>();
        services.AddScoped<IShopReadService, ShopReadService>();
        services.AddScoped<IVehicleDocumentRepository, VehicleDocumentRepository>();
        services.AddScoped<IVehicleDocumentReadService, VehicleDocumentReadService>();
        services.AddScoped<IServiceRecordRepository, ServiceRecordRepository>();
        services.AddScoped<IServiceRecordReadService, ServiceRecordReadService>();
        services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
        services.AddScoped<IWorkOrderReadService, WorkOrderReadService>();

        // 3. Command/query handlers — registered explicitly, one line per handler.
        services.AddScoped<ICommandHandler<RegisterVehicleCommand, Guid>, RegisterVehicleCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateVehicleCommand>, UpdateVehicleCommandHandler>();
        services.AddScoped<ICommandHandler<ChangeVehicleStatusCommand>, ChangeVehicleStatusCommandHandler>();
        services.AddScoped<ICommandHandler<RecordOdometerCommand>, RecordOdometerCommandHandler>();
        services.AddScoped<ICommandHandler<DisposeVehicleCommand>, DisposeVehicleCommandHandler>();
        services.AddScoped<ICommandHandler<EnsureRetirementCertificateCommand>, EnsureRetirementCertificateCommandHandler>();
        services.AddScoped<IQueryHandler<GetVehiclesQuery, IReadOnlyList<VehicleResponse>>, GetVehiclesQueryHandler>();
        services.AddScoped<IQueryHandler<GetVehicleByIdQuery, VehicleResponse>, GetVehicleByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetRetirementCertificateQuery, RetirementCertificateResponse>, GetRetirementCertificateQueryHandler>();
        services.AddScoped<IQueryHandler<GetVehicleInspectionsQuery, IReadOnlyList<VehicleInspectionResponse>>, GetVehicleInspectionsQueryHandler>();
        services.AddScoped<ICommandHandler<EnterInspectionCommand, Guid>, EnterInspectionCommandHandler>();
        services.AddScoped<ICommandHandler<RegisterShopCommand, Guid>, RegisterShopCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateShopCommand>, UpdateShopCommandHandler>();
        services.AddScoped<IQueryHandler<GetShopsQuery, IReadOnlyList<ShopResponse>>, GetShopsQueryHandler>();
        services.AddScoped<ICommandHandler<AddVehicleDocumentCommand, Guid>, AddVehicleDocumentCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveVehicleDocumentCommand>, RemoveVehicleDocumentCommandHandler>();
        services.AddScoped<IQueryHandler<GetVehicleDocumentsQuery, IReadOnlyList<VehicleDocumentResponse>>, GetVehicleDocumentsQueryHandler>();
        services.AddScoped<IQueryHandler<GetAllDocumentsQuery, IReadOnlyList<VehicleDocumentResponse>>, GetAllDocumentsQueryHandler>();
        services.AddScoped<ICommandHandler<AddServiceRecordCommand, Guid>, AddServiceRecordCommandHandler>();
        services.AddScoped<IQueryHandler<GetVehicleServiceRecordsQuery, IReadOnlyList<ServiceRecordResponse>>, GetVehicleServiceRecordsQueryHandler>();
        services.AddScoped<ICommandHandler<CreateWorkOrderCommand, Guid>, CreateWorkOrderCommandHandler>();
        services.AddScoped<ICommandHandler<ChangeWorkOrderStatusCommand>, ChangeWorkOrderStatusCommandHandler>();
        services.AddScoped<ICommandHandler<CompleteWorkOrderCommand, Guid>, CompleteWorkOrderCommandHandler>();
        services.AddScoped<IQueryHandler<GetVehicleWorkOrdersQuery, IReadOnlyList<WorkOrderResponse>>, GetVehicleWorkOrdersQueryHandler>();
        services.AddScoped<IQueryHandler<GetAllWorkOrdersQuery, IReadOnlyList<WorkOrderResponse>>, GetAllWorkOrdersQueryHandler>();

        // 4. Integration event consumers — a completed trip manifest materializes the
        //    pre- and post-trip inspection records (cross-domain, events-only).
        services.AddIntegrationEventConsumer(SchemaName, subscriptions => subscriptions
            .On<TripManifestCompletedIntegrationEvent, TripManifestCompletedIntegrationEventHandler>());

        // 5. Read-side projections — one worker polls fleet.event_journal, refreshes the
        //    matviews the batch touched, and dispatches same-module secondary commands.
        //    Retirement certificates are driven by vehicle events (they're created inline
        //    during a vehicle's retirement, so they share the "vehicle" aggregate's journal).
        services.AddProjections<FleetDbContext>(SchemaName, registry => registry
            .OnAggregate("vehicle", "mv_vehicles", "mv_retirement_certificates")
            .OnAggregate("shop", "mv_shops")
            .OnAggregate("vehicle-document", "mv_vehicle_documents")
            .OnAggregate("service-record", "mv_service_records")
            .OnAggregate("work-order", "mv_work_orders")
            .OnAggregate("vehicle-inspection", "mv_vehicle_inspections")
            .OnEvent<VehicleReachedEndOfLifeDomainEvent>(entry =>
                new EnsureRetirementCertificateCommand(entry.AggregateId)));

        return services;
    }

    /// <summary>
    /// Applies pending Fleet migrations and, unless <c>DevSeed:IncludeDemoData</c> is set to
    /// false, seeds the demo vehicles for <paramref name="tenantId"/>. The API host calls
    /// this in Development only. <paramref name="tenantId"/> is passed explicitly rather than
    /// resolved via <see cref="ITenantContext"/> — that interface now reflects the caller's
    /// JWT, which doesn't exist yet at startup.
    /// </summary>
    public static async Task InitializeFleetDatabaseAsync(this IServiceProvider serviceProvider, Guid tenantId)
    {
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        await context.Database.MigrateAsync();

        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (configuration.GetValue("DevSeed:IncludeDemoData", true))
        {
            await FleetDevSeeder.SeedAsync(context, tenantId);
        }
    }
}
