using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Shared.Persistence.Migrations;

/// <summary>The set of module DbContexts <see cref="ModuleMigrationRunner"/> walks, in order.</summary>
public sealed class MigrationTargets(IReadOnlyList<Type> contextTypes)
{
    public IReadOnlyList<Type> ContextTypes { get; } = contextTypes;
}

public static class MigrationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the startup migration runner over the given module DbContexts. Called once by the
    /// gateway — migration policy is a host concern, like health endpoints, not something each
    /// domain library decides for itself. Registering it there also means it starts ahead of every
    /// module's <c>OutboxDispatcher</c>, since hosted services start in registration order.
    ///
    /// Registration itself touches no environment variables (the connection string is read inside
    /// the runner), so DI-registration tests can invoke the composition root bare.
    /// </summary>
    public static IServiceCollection AddModuleMigrations(
        this IServiceCollection services,
        IConfiguration configuration,
        params Type[] contextTypes)
    {
        foreach (var contextType in contextTypes)
        {
            if (!typeof(ModuleDbContext).IsAssignableFrom(contextType))
            {
                throw new ArgumentException(
                    $"{contextType.Name} is not a {nameof(ModuleDbContext)} — only module DbContexts carry a per-schema migrations history.",
                    nameof(contextTypes));
            }
        }

        var options = configuration.GetSection(MigrationOptions.SectionName).Get<MigrationOptions>()
            ?? new MigrationOptions();

        services.AddSingleton(options);
        services.AddSingleton(new MigrationTargets(contextTypes));
        services.AddSingleton<MigrationState>();
        services.AddHostedService<ModuleMigrationRunner>();

        return services;
    }
}
