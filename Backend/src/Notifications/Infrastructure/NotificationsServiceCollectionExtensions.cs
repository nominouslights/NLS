using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Notifications.Application;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Dispatches;
using NorthernLink.Notifications.Application.Dispatches.GetTripEmailHistory;
using NorthernLink.Notifications.Application.Dispatches.SendTripPickupEmail;
using NorthernLink.Notifications.Application.Templates;
using NorthernLink.Notifications.Application.Templates.Activate;
using NorthernLink.Notifications.Application.Templates.Create;
using NorthernLink.Notifications.Application.Templates.Deactivate;
using NorthernLink.Notifications.Application.Templates.GetTemplateById;
using NorthernLink.Notifications.Application.Templates.GetTemplates;
using NorthernLink.Notifications.Application.Templates.Preview;
using NorthernLink.Notifications.Application.Templates.Update;
using NorthernLink.Notifications.Infrastructure.Email;
using NorthernLink.Notifications.Infrastructure.Persistence;
using NorthernLink.Notifications.Infrastructure.Persistence.Projections;
using NorthernLink.Notifications.Infrastructure.Rendering;

namespace NorthernLink.Notifications.Infrastructure;

/// <summary>
/// DI entry point for the Notifications domain library — the only thing the API gateway sees.
/// Registers the library DbContext (Postgres schema "notifications"), persistence services,
/// the Postmark email sender, and every CQRS handler explicitly
/// (the reflection-based Sender resolves handlers from DI; no assembly scanning).
/// </summary>
public static class NotificationsServiceCollectionExtensions
{
    public const string SchemaName = "notifications";

    public static IServiceCollection AddNotifications(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. DbContext — module schema, tenant session interceptor (RLS session variable).
        //    TryAdd: the interceptor is shared platform plumbing other modules also register.
        //    The connection-string lookup stays inside the factory lambda — registration
        //    itself must need no environment (HandlerRegistrationRulesTests invokes it bare).
        services.TryAddScoped<TenantSessionInterceptor>();

        services.AddDbContext<NotificationsDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(
                    RequiredEnvironmentVariable.Get("ConnectionStrings__Postgres"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName))
                .AddInterceptors(serviceProvider.GetRequiredService<TenantSessionInterceptor>()));

        // 2. Persistence + read services. The mapper is registered as its concrete type —
        //    every module has its own IIntegrationEventMapper, so the interface can't be
        //    a single DI registration. The outbox dispatcher drains notifications.outbox_messages.
        services.AddScoped<NotificationsIntegrationEventMapper>();
        services.AddHostedService<OutboxDispatcher<NotificationsDbContext>>();
        services.AddScoped<IEmailTemplateRepository, EmailTemplateRepository>();
        services.AddScoped<IEmailTemplateReadService, EmailTemplateReadService>();
        services.AddScoped<IEmailDispatchRepository, EmailDispatchRepository>();
        services.AddScoped<IEmailDispatchReadService, EmailDispatchReadService>();

        // Allowlist HTML sanitizer for stored/sent template bodies. Singleton: the underlying
        // Ganss HtmlSanitizer is configured once and each Sanitize call is a self-contained parse.
        services.AddSingleton<IEmailHtmlSanitizer, GanssEmailHtmlSanitizer>();

        // 3. Postmark sender over IHttpClientFactory (Microsoft.Extensions.Http ships in the
        //    AspNetCore framework reference — no extra package, no Polly, no SDK). Options are
        //    non-secret and config-bound; the server token is an env var read on the send path.
        var notificationsOptions = configuration.GetSection(NotificationsOptions.SectionName).Get<NotificationsOptions>()
            ?? new NotificationsOptions();
        services.AddSingleton(notificationsOptions);

        services.AddHttpClient<IEmailSender, PostmarkEmailSender>(client =>
        {
            client.BaseAddress = new Uri(notificationsOptions.PostmarkBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // 4. Command/query handlers — registered explicitly, one line per handler.
        services.AddScoped<ICommandHandler<CreateEmailTemplateCommand, Guid>, CreateEmailTemplateCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateEmailTemplateCommand>, UpdateEmailTemplateCommandHandler>();
        services.AddScoped<ICommandHandler<ActivateEmailTemplateCommand>, ActivateEmailTemplateCommandHandler>();
        services.AddScoped<ICommandHandler<DeactivateEmailTemplateCommand>, DeactivateEmailTemplateCommandHandler>();
        services.AddScoped<IQueryHandler<GetEmailTemplatesQuery, IReadOnlyList<EmailTemplateResponse>>, GetEmailTemplatesQueryHandler>();
        services.AddScoped<IQueryHandler<GetEmailTemplateByIdQuery, EmailTemplateResponse>, GetEmailTemplateByIdQueryHandler>();
        services.AddScoped<IQueryHandler<PreviewEmailTemplateQuery, EmailTemplatePreviewResponse>, PreviewEmailTemplateQueryHandler>();
        services.AddScoped<ICommandHandler<SendTripPickupEmailCommand, EmailDispatchResponse>, SendTripPickupEmailCommandHandler>();
        services.AddScoped<IQueryHandler<GetTripEmailHistoryQuery, IReadOnlyList<EmailDispatchResponse>>, GetTripEmailHistoryQueryHandler>();

        // 5. Integration event consumers — none: Notifications neither publishes nor consumes
        //    today (the send request carries its trip context as opaque snapshots).

        // 6. Read-side projections — one worker polls notifications.event_journal and upserts
        //    the read-model rows for the aggregates the batch touched.
        services.AddProjections<NotificationsDbContext>(SchemaName, registry => registry
            .Project(new EmailTemplateProjection())
            .Project(new EmailDispatchProjection()));

        return services;
    }
}
