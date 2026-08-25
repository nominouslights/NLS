using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
using NorthernLink.Fleet.Application.Inspections.PropagateOdometer;
using NorthernLink.Fleet.Application.Inspections.Remove;
using NorthernLink.Fleet.Application.Inspections.Update;
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
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Fleet.Domain.Inspections.Events;
using NorthernLink.Fleet.Domain.Vehicles.Events;
using NorthernLink.Fleet.Infrastructure.Persistence;
using NorthernLink.Fleet.Infrastructure.Persistence.Projections;

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
                    RequiredEnvironmentVariable.Get("ConnectionStrings__Postgres"),
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
        services.AddScoped<IMaintenancePlanRepository, MaintenancePlanRepository>();
        services.AddScoped<IPlanAssignmentRepository, PlanAssignmentRepository>();
        services.AddScoped<IPmCompletionRepository, PmCompletionRepository>();

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
        services.AddScoped<ICommandHandler<UpdateInspectionCommand>, UpdateInspectionCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveInspectionCommand>, RemoveInspectionCommandHandler>();
        services.AddScoped<ICommandHandler<PropagateInspectionOdometerCommand>, PropagateInspectionOdometerCommandHandler>();
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

        // 4. Read-side projections — one worker polls fleet.event_journal, upserts the read-model
        //    rows for the aggregates the batch touched, and dispatches same-module secondary
        //    commands. Retirement certificates are driven by vehicle events (they're created
        //    inline during a vehicle's retirement, so they share the "vehicle" aggregate's
        //    journal) — hence two projections keyed on the same aggregate type. A newly entered
        //    inspection advances the linked vehicle's odometer intra-Fleet (monotonic, auto-retire
        //    applies) — the same same-module reaction pattern.
        services.AddProjections<FleetDbContext>(SchemaName, registry => registry
            .Project(new VehicleProjection())
            .Project(new RetirementCertificateProjection())
            .Project(new ShopProjection())
            .Project(new VehicleDocumentProjection())
            .Project(new ServiceRecordProjection())
            .Project(new WorkOrderProjection())
            .Project(new VehicleInspectionProjection())
            .Project(new MaintenancePlanProjection())
            .Project(new PlanAssignmentProjection())
            .Project(new PmCompletionProjection())
            .OnEvent<VehicleReachedEndOfLifeDomainEvent>(entry =>
                new EnsureRetirementCertificateCommand(entry.AggregateId))
            .OnEvent<VehicleInspectionCreatedDomainEvent>(entry =>
                new PropagateInspectionOdometerCommand(entry.TenantId, entry.AggregateId))
            // A corrected odometer still flows to the vehicle. The vehicle odometer is monotonic
            // (Vehicle.RecordOdometer no-ops a non-advancing reading), so a downward correction
            // will not roll the vehicle back — acceptable: the inspection remains the record of
            // what was actually read.
            .OnEvent<VehicleInspectionAmendedDomainEvent>(entry =>
                new PropagateInspectionOdometerCommand(entry.TenantId, entry.AggregateId)));

        return services;
    }

}
