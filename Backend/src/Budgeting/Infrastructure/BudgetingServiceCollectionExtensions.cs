using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.IntegrationEvents.Identity;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Budgeting.Application;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Integration;
using NorthernLink.Budgeting.Application.Codes;
using NorthernLink.Budgeting.Application.Codes.Create;
using NorthernLink.Budgeting.Application.Codes.Delete;
using NorthernLink.Budgeting.Application.Codes.GetCodes;
using NorthernLink.Budgeting.Application.Codes.GetOwnerCandidates;
using NorthernLink.Budgeting.Application.Codes.SeedStarterSet;
using NorthernLink.Budgeting.Application.Codes.SetActive;
using NorthernLink.Budgeting.Application.Codes.Update;
using NorthernLink.Budgeting.Application.Periods;
using NorthernLink.Budgeting.Application.Periods.Create;
using NorthernLink.Budgeting.Application.Periods.GetPeriods;
using NorthernLink.Budgeting.Infrastructure.Persistence;
using NorthernLink.Budgeting.Infrastructure.Persistence.Projections;

namespace NorthernLink.Budgeting.Infrastructure;

/// <summary>
/// DI entry point for the Budgeting domain library — the only thing the API gateway sees.
/// Registers the library DbContext (Postgres schema "budgeting"), persistence services,
/// and every CQRS handler explicitly
/// (the reflection-based Sender resolves handlers from DI; no assembly scanning).
/// </summary>
public static class BudgetingServiceCollectionExtensions
{
    public const string SchemaName = "budgeting";

    public static IServiceCollection AddBudgeting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. DbContext — module schema, tenant session interceptor (RLS session variable).
        //    TryAdd: the interceptor is shared platform plumbing other modules also register.
        //    The connection-string lookup stays inside the factory lambda — registration
        //    itself must need no environment (HandlerRegistrationRulesTests invokes it bare).
        services.TryAddScoped<TenantSessionInterceptor>();

        services.AddDbContext<BudgetingDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(
                    RequiredEnvironmentVariable.Get("ConnectionStrings__Postgres"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName))
                .AddInterceptors(serviceProvider.GetRequiredService<TenantSessionInterceptor>()));

        // 2. Persistence + read services. The mapper is registered as its concrete type —
        //    every module has its own IIntegrationEventMapper, so the interface can't be
        //    a single DI registration. The outbox dispatcher drains budgeting.outbox_messages.
        services.AddScoped<BudgetingIntegrationEventMapper>();
        services.AddHostedService<OutboxDispatcher<BudgetingDbContext>>();
        services.AddScoped<IBudgetPeriodRepository, BudgetPeriodRepository>();
        services.AddScoped<IBudgetPeriodReadService, BudgetPeriodReadService>();
        services.AddScoped<IBudgetCodeRepository, BudgetCodeRepository>();
        services.AddScoped<IBudgetCodeReadService, BudgetCodeReadService>();
        services.AddScoped<IUserLookupRepository, UserLookupRepository>();

        // Nothing references a budget code yet, so "never referenced" is the true answer, not a
        // stub. Stage 6.2 swaps in the allocation-aware implementation — see the class comment.
        services.AddScoped<IBudgetCodeUsageProbe, NeverReferencedBudgetCodeUsageProbe>();

        // 3. Command/query handlers — registered explicitly, one line per handler.
        services.AddScoped<ICommandHandler<CreateBudgetPeriodCommand, Guid>, CreateBudgetPeriodCommandHandler>();
        services.AddScoped<IQueryHandler<GetBudgetPeriodsQuery, IReadOnlyList<BudgetPeriodResponse>>, GetBudgetPeriodsQueryHandler>();
        services.AddScoped<ICommandHandler<CreateBudgetCodeCommand, Guid>, CreateBudgetCodeCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateBudgetCodeCommand>, UpdateBudgetCodeCommandHandler>();
        services.AddScoped<ICommandHandler<SetBudgetCodeActiveCommand>, SetBudgetCodeActiveCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteBudgetCodeCommand>, DeleteBudgetCodeCommandHandler>();
        services.AddScoped<ICommandHandler<SeedStarterBudgetCodesCommand, int>, SeedStarterBudgetCodesCommandHandler>();
        services.AddScoped<IQueryHandler<GetBudgetCodesQuery, IReadOnlyList<BudgetCodeResponse>>, GetBudgetCodesQueryHandler>();
        services.AddScoped<IQueryHandler<GetBudgetOwnerCandidatesQuery, IReadOnlyList<BudgetOwnerOptionResponse>>, GetBudgetOwnerCandidatesQueryHandler>();

        // 4. Integration event consumers — the Identity replica that keeps user_lookup current,
        //    so a budget code can name an accountable owner and its created_by/modified_by
        //    columns resolve to a readable email without referencing the Identity library.
        //    Budgeting still publishes nothing: periods and codes are this module's private
        //    state, and the free-text budget_code strings Clients and Fleet carry are theirs,
        //    not replicas of these rows.
        //
        //    Note the usual replay-the-whole-outbox-history behaviour buys nothing here:
        //    identity.outbox_messages was empty until Identity got its first mapper, so every
        //    user predating that lands via the BackfillBudgetingUserLookup migration instead.
        services.AddOutboxPollingConsumer<BudgetingDbContext>(SchemaName, subscriptions => subscriptions
            .On<UserChangedIntegrationEvent, UserChangedIntegrationEventHandler>());

        // 5. Read-side projections — one worker polls budgeting.event_journal and upserts
        //    the read-model rows for the aggregates the batch touched.
        services.AddProjections<BudgetingDbContext>(SchemaName, registry => registry
            .Project(new BudgetPeriodProjection())
            .Project(new BudgetCodeProjection()));

        return services;
    }
}
