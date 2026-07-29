using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Application.BillableTrips;
using NorthernLink.Billing.Application.BillableTrips.GetBillableTrips;
using NorthernLink.Billing.Application.Integration;
using NorthernLink.Billing.Application.Invoices;
using NorthernLink.Billing.Application.Invoices.GenerateDraft;
using NorthernLink.Billing.Application.Invoices.GetById;
using NorthernLink.Billing.Application.Invoices.GetInvoices;
using NorthernLink.Billing.Application.Invoices.MarkEntered;
using NorthernLink.Billing.Application.Invoices.ReplaceLines;
using NorthernLink.Billing.Application.Invoices.Reopen;
using NorthernLink.Billing.Application.Invoices.UpdateQboReference;
using NorthernLink.Billing.Application.Invoices.Void;
using NorthernLink.Billing.Infrastructure.Persistence;
using NorthernLink.Billing.Infrastructure.Persistence.Projections;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.IntegrationEvents.Clients;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Billing.Infrastructure;

/// <summary>
/// DI entry point for the Billing domain library — the only thing the API gateway sees.
/// Registers the library DbContext (Postgres schema "billing"), persistence services,
/// every CQRS handler explicitly (the reflection-based Sender resolves handlers from DI;
/// no assembly scanning), the two replica-maintaining integration-event consumers, and
/// the invoice read-model projection.
/// </summary>
public static class BillingServiceCollectionExtensions
{
    public const string SchemaName = "billing";

    public static IServiceCollection AddBilling(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. DbContext — module schema, tenant session interceptor (RLS session variable).
        //    TryAdd: the interceptor is shared platform plumbing other modules also register.
        services.TryAddScoped<TenantSessionInterceptor>();

        services.AddDbContext<BillingDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(
                    RequiredEnvironmentVariable.Get("ConnectionStrings__Postgres"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName))
                .AddInterceptors(serviceProvider.GetRequiredService<TenantSessionInterceptor>()));

        // 2. Persistence + read services. The mapper is registered as its concrete type —
        //    every module has its own IIntegrationEventMapper, so the interface can't be
        //    a single DI registration. The outbox dispatcher drains billing.outbox_messages
        //    (empty until Billing has a public event surface, but registered now so it never
        //    silently backs up later).
        services.AddScoped<BillingIntegrationEventMapper>();
        services.AddHostedService<OutboxDispatcher<BillingDbContext>>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IContractSnapshotRepository, ContractSnapshotRepository>();
        services.AddScoped<IBillableTripRepository, BillableTripRepository>();
        services.AddScoped<IInvoiceNumberGenerator, InvoiceNumberGenerator>();
        services.AddScoped<IInvoiceReadService, InvoiceReadService>();
        services.AddScoped<IBillableTripReadService, BillableTripReadService>();

        // 3. Command/query handlers — registered explicitly, one line per handler.
        services.AddScoped<ICommandHandler<GenerateDraftInvoiceCommand, Guid>, GenerateDraftInvoiceCommandHandler>();
        services.AddScoped<ICommandHandler<ReplaceInvoiceLinesCommand>, ReplaceInvoiceLinesCommandHandler>();
        services.AddScoped<ICommandHandler<MarkInvoiceEnteredCommand>, MarkInvoiceEnteredCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateInvoiceQboReferenceCommand>, UpdateInvoiceQboReferenceCommandHandler>();
        services.AddScoped<ICommandHandler<ReopenInvoiceCommand>, ReopenInvoiceCommandHandler>();
        services.AddScoped<ICommandHandler<VoidInvoiceCommand>, VoidInvoiceCommandHandler>();
        services.AddScoped<IQueryHandler<GetInvoicesQuery, IReadOnlyList<InvoiceSummaryResponse>>, GetInvoicesQueryHandler>();
        services.AddScoped<IQueryHandler<GetInvoiceByIdQuery, InvoiceResponse>, GetInvoiceByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetBillableTripsQuery, IReadOnlyList<BillableTripResponse>>, GetBillableTripsQueryHandler>();

        // 4. Integration event consumers — the module's replicas: contract snapshots from
        //    Clients, billable trips from Trips (cross-domain, events-only; both idempotent).
        //    Storing/projecting events, so they arrive by polling the producer outboxes
        //    in-database, not via RabbitMQ.
        services.AddOutboxPollingConsumer<BillingDbContext>(SchemaName, subscriptions => subscriptions
            .On<ContractChangedIntegrationEvent, ContractChangedIntegrationEventHandler>()
            .On<TripCompletedIntegrationEvent, TripCompletedIntegrationEventHandler>()
            .On<TripRoundTripChangedIntegrationEvent, TripRoundTripChangedIntegrationEventHandler>());

        // 5. Read-side projections — one worker polls billing.event_journal and upserts
        //    rm_invoices for the invoices each batch touched.
        services.AddProjections<BillingDbContext>(SchemaName, registry => registry
            .Project(new InvoiceProjection()));

        return services;
    }
}
