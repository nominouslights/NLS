using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Persistence;

namespace NorthernLink.Shared.Persistence.Projections;

public static class ProjectionServiceCollectionExtensions
{
    /// <summary>
    /// Declares a module's read-side projections: builds the <see cref="IProjectionRegistry"/>
    /// from the fluent configuration and registers one hosted
    /// <see cref="ProjectionWorker{TDbContext}"/> that keeps the module's matviews fresh and
    /// dispatches its secondary commands. Called from the module's DI extension beside its
    /// outbox dispatcher; mirrors <c>AddIntegrationEventConsumer</c>'s registration shape.
    /// </summary>
    public static IServiceCollection AddProjections<TDbContext>(
        this IServiceCollection services,
        string schema,
        Action<ProjectionRegistryBuilder> configure)
        where TDbContext : ModuleDbContext
    {
        var builder = new ProjectionRegistryBuilder(schema);
        configure(builder);
        var registry = builder.Build();

        services.AddSingleton<IHostedService>(serviceProvider => new ProjectionWorker<TDbContext>(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            serviceProvider.GetRequiredService<ProjectionOptions>(),
            serviceProvider.GetRequiredService<ILogger<ProjectionWorker<TDbContext>>>()));

        return services;
    }
}
