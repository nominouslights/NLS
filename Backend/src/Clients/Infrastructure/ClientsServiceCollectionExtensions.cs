using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Application.Clients;
using NorthernLink.Clients.Application.Clients.Create;
using NorthernLink.Clients.Application.Clients.GetClientById;
using NorthernLink.Clients.Application.Clients.GetClients;
using NorthernLink.Clients.Application.Clients.Update;
using NorthernLink.Clients.Application.ClientContacts;
using NorthernLink.Clients.Application.ClientContacts.Create;
using NorthernLink.Clients.Application.ClientContacts.GetForClient;
using NorthernLink.Clients.Application.ClientContacts.Update;
using NorthernLink.Clients.Application.Contracts;
using NorthernLink.Clients.Application.Contracts.Create;
using NorthernLink.Clients.Application.Contracts.GetForClient;
using NorthernLink.Clients.Application.Contracts.Terminate;
using NorthernLink.Clients.Application.Contracts.Update;
using NorthernLink.Clients.Application.PurchaseOrders;
using NorthernLink.Clients.Application.PurchaseOrders.Create;
using NorthernLink.Clients.Application.PurchaseOrders.Delete;
using NorthernLink.Clients.Application.PurchaseOrders.GetForClient;
using NorthernLink.Clients.Application.PurchaseOrders.Update;
using NorthernLink.Clients.Infrastructure.Persistence;
using NorthernLink.Clients.Infrastructure.Persistence.Projections;

namespace NorthernLink.Clients.Infrastructure;

/// <summary>
/// DI entry point for the Clients domain library — the only thing the API gateway sees.
/// Registers the library DbContext (Postgres schema "clients"), persistence services,
/// and every CQRS handler explicitly
/// (the reflection-based Sender resolves handlers from DI; no assembly scanning).
/// </summary>
public static class ClientsServiceCollectionExtensions
{
    public const string SchemaName = "clients";

    public static IServiceCollection AddClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. DbContext — module schema, tenant session interceptor (RLS session variable).
        //    TryAdd: the interceptor is shared platform plumbing other modules also register.
        services.TryAddScoped<TenantSessionInterceptor>();

        services.AddDbContext<ClientsDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(
                    RequiredEnvironmentVariable.Get("ConnectionStrings__Postgres"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName))
                .AddInterceptors(serviceProvider.GetRequiredService<TenantSessionInterceptor>()));

        // 2. Persistence + read services. The mapper is registered as its concrete type —
        //    every module has its own IIntegrationEventMapper, so the interface can't be
        //    a single DI registration. The outbox dispatcher drains clients.outbox_messages.
        services.AddScoped<ClientsIntegrationEventMapper>();
        services.AddHostedService<OutboxDispatcher<ClientsDbContext>>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IClientReadService, ClientReadService>();
        services.AddScoped<IClientContactRepository, ClientContactRepository>();
        services.AddScoped<IClientContactReadService, ClientContactReadService>();
        services.AddScoped<IContractRepository, ContractRepository>();
        services.AddScoped<IContractReadService, ContractReadService>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<IPurchaseOrderReadService, PurchaseOrderReadService>();

        // 3. Command/query handlers — registered explicitly, one line per handler.
        services.AddScoped<ICommandHandler<CreateClientCommand, Guid>, CreateClientCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateClientCommand>, UpdateClientCommandHandler>();
        services.AddScoped<IQueryHandler<GetClientsQuery, IReadOnlyList<ClientResponse>>, GetClientsQueryHandler>();
        services.AddScoped<IQueryHandler<GetClientByIdQuery, ClientResponse>, GetClientByIdQueryHandler>();
        services.AddScoped<ICommandHandler<CreateClientContactCommand, Guid>, CreateClientContactCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateClientContactCommand>, UpdateClientContactCommandHandler>();
        services.AddScoped<IQueryHandler<GetClientContactsQuery, IReadOnlyList<ClientContactResponse>>, GetClientContactsQueryHandler>();
        services.AddScoped<ICommandHandler<CreateContractCommand, Guid>, CreateContractCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateContractCommand>, UpdateContractCommandHandler>();
        services.AddScoped<ICommandHandler<TerminateContractCommand>, TerminateContractCommandHandler>();
        services.AddScoped<IQueryHandler<GetClientContractsQuery, IReadOnlyList<ContractResponse>>, GetClientContractsQueryHandler>();
        services.AddScoped<ICommandHandler<CreatePurchaseOrderCommand, Guid>, CreatePurchaseOrderCommandHandler>();
        services.AddScoped<ICommandHandler<UpdatePurchaseOrderCommand>, UpdatePurchaseOrderCommandHandler>();
        services.AddScoped<ICommandHandler<DeletePurchaseOrderCommand>, DeletePurchaseOrderCommandHandler>();
        services.AddScoped<IQueryHandler<GetClientPurchaseOrdersQuery, IReadOnlyList<PurchaseOrderResponse>>, GetClientPurchaseOrdersQueryHandler>();

        // 4. Integration event consumers — none: Clients only publishes (client-changed,
        //    contract-changed); it consumes nothing from other modules today.

        // 5. Read-side projections — one worker polls clients.event_journal and upserts the
        //    read-model rows for the aggregates the batch touched. Contract journal rows drive
        //    two projections: rm_contracts itself, and the denormalized active-contract summary
        //    columns on rm_clients (the Fleet retirement-certificate idiom of a second
        //    projection riding another aggregate's journal).
        services.AddProjections<ClientsDbContext>(SchemaName, registry => registry
            .Project(new ClientProjection())
            .Project(new ClientContactProjection())
            .Project(new ContractProjection())
            .Project(new ClientContractSummaryProjection())
            .Project(new PurchaseOrderProjection()));

        return services;
    }
}
