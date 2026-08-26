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
using NorthernLink.Fleet.Application.Maintenance;
using NorthernLink.Fleet.Application.Maintenance.Assignments.Assign;
using NorthernLink.Fleet.Application.Maintenance.Assignments.Unassign;
using NorthernLink.Fleet.Application.Maintenance.Completions.Log;
using NorthernLink.Fleet.Application.Maintenance.Completions.PropagateOdometer;
using NorthernLink.Fleet.Application.Maintenance.Plans.Create;
using NorthernLink.Fleet.Application.Maintenance.Plans.GetAll;
using NorthernLink.Fleet.Application.Maintenance.Plans.GetById;
using NorthernLink.Fleet.Application.Maintenance.Plans.SeedDefaults;
using NorthernLink.Fleet.Application.Maintenance.Plans.Update;
using NorthernLink.Fleet.Application.Maintenance.Status.GetDue;
using NorthernLink.Fleet.Application.Maintenance.Status.GetFleetDue;
using NorthernLink.Fleet.Application.Maintenance.Status.GetHistory;
using NorthernLink.Fleet.Application.Maintenance.Status.GetOverhauls;
using NorthernLink.Fleet.Application.Maintenance.Status.GetVehicleStatus;
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
        services.AddScoped<IPmReadService, PmReadService>();

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
        services.AddScoped<ICommandHandler<CreateMaintenancePlanCommand, Guid>, CreateMaintenancePlanCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateMaintenancePlanCommand>, UpdateMaintenancePlanCommandHandler>();
        services.AddScoped<ICommandHandler<SeedDefaultMaintenancePlanCommand, Guid>, SeedDefaultMaintenancePlanCommandHandler>();
        services.AddScoped<IQueryHandler<GetMaintenancePlansQuery, IReadOnlyList<MaintenancePlanSummaryResponse>>, GetMaintenancePlansQueryHandler>();
        services.AddScoped<IQueryHandler<GetMaintenancePlanByIdQuery, MaintenancePlanResponse>, GetMaintenancePlanByIdQueryHandler>();
        services.AddScoped<ICommandHandler<AssignMaintenancePlanCommand>, AssignMaintenancePlanCommandHandler>();
        services.AddScoped<ICommandHandler<UnassignMaintenancePlanCommand>, UnassignMaintenancePlanCommandHandler>();
        services.AddScoped<ICommandHandler<LogPmCompletionCommand, Guid>, LogPmCompletionCommandHandler>();
        services.AddScoped<ICommandHandler<PropagatePmOdometerCommand>, PropagatePmOdometerCommandHandler>();
        services.AddScoped<IQueryHandler<GetVehiclePmStatusQuery, VehiclePmStatusResponse>, GetVehiclePmStatusQueryHandler>();
        services.AddScoped<IQueryHandler<GetPmDueQuery, PmDueResponse>, GetPmDueQueryHandler>();
        services.AddScoped<IQueryHandler<GetPmOverhaulsQuery, PmOverhaulsResponse>, GetPmOverhaulsQueryHandler>();
        services.AddScoped<IQueryHandler<GetPmHistoryQuery, IReadOnlyList<PmCompletionResponse>>, GetPmHistoryQueryHandler>();
        services.AddScoped<IQueryHandler<GetFleetPmDueQuery, FleetPmDueResponse>, GetFleetPmDueQueryHandler>();

        // 4. Read-side projections — one worker polls fleet.event_journal, upserts the read-model
        //    rows for the aggregates the batch touched, and dispatches same-module secondary
        //    commands. The registry contents (every projection + OnEvent reaction) live in
        //    FleetProjectionRegistry.Configure, shared with the integration-test fixture so the
        //    tests always exercise exactly the read side the API composes.
        services.AddProjections<FleetDbContext>(SchemaName, FleetProjectionRegistry.Configure);

        return services;
    }

}
