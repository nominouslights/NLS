using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Identity.Application.Auth;
using NorthernLink.Identity.Application.Auth.BootstrapAdmin;
using NorthernLink.Identity.Application.Auth.GenerateBootstrapToken;
using NorthernLink.Identity.Application.Auth.Login;
using NorthernLink.Identity.Application.Auth.Logout;
using NorthernLink.Identity.Application.Auth.Refresh;
using NorthernLink.Identity.Infrastructure.Auth;
using NorthernLink.Identity.Infrastructure.DevSeed;
using NorthernLink.Identity.Infrastructure.Persistence;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Identity.Infrastructure;

/// <summary>
/// DI entry point for the Identity domain library — the only thing the API gateway sees.
/// Registers the library DbContext (Postgres schema "identity"), persistence services, and
/// every CQRS handler explicitly (the reflection-based Sender resolves handlers from DI; no
/// assembly scanning) — same shape as <c>FleetServiceCollectionExtensions</c>.
/// </summary>
public static class IdentityServiceCollectionExtensions
{
    public const string SchemaName = "identity";

    public static IServiceCollection AddIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. DbContext — module schema, tenant session interceptor (RLS session variable).
        //    TryAdd: the interceptor is shared platform plumbing other modules also register.
        services.TryAddScoped<TenantSessionInterceptor>();

        services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(
                    configuration.GetConnectionString("Postgres"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName))
                .AddInterceptors(serviceProvider.GetRequiredService<TenantSessionInterceptor>()));

        // 2. Persistence + auth services. No IIntegrationEventMapper is registered yet — no
        //    Identity domain event has a public integration-event contract today — so the
        //    outbox dispatcher just polls an always-empty table, same as every other module.
        services.AddHostedService<OutboxDispatcher<IdentityDbContext>>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAdminBootstrapTokenRepository, AdminBootstrapTokenRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        // 3. Command handlers — registered explicitly, one line per handler.
        services.AddScoped<ICommandHandler<LoginCommand, LoginResponse>, LoginCommandHandler>();
        services.AddScoped<ICommandHandler<RefreshTokenCommand, LoginResponse>, RefreshTokenCommandHandler>();
        services.AddScoped<ICommandHandler<LogoutCommand>, LogoutCommandHandler>();
        services.AddScoped<ICommandHandler<BootstrapAdminCommand, Guid>, BootstrapAdminCommandHandler>();
        services.AddScoped<ICommandHandler<GenerateBootstrapTokenCommand, GenerateBootstrapTokenResponse>, GenerateBootstrapTokenCommandHandler>();

        return services;
    }

    /// <summary>
    /// Applies pending Identity migrations and seeds the one initial admin account. Unlike
    /// Fleet/Trips' dev-only demo seeding, this runs in every environment — a platform with
    /// no way to log in isn't usable anywhere, dev or otherwise.
    /// </summary>
    public static async Task InitializeIdentityDatabaseAsync(this IServiceProvider serviceProvider, Guid tenantId)
    {
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await context.Database.MigrateAsync();

        var logger = scope.ServiceProvider.GetService<ILogger<IdentityDbContext>>();
        await IdentityDevSeeder.SeedAsync(context, tenantId, logger);
    }
}
