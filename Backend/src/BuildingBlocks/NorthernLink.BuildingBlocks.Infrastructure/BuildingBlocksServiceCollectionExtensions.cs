using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NorthernLink.BuildingBlocks.Application.Events;
using NorthernLink.BuildingBlocks.Application.Messaging;
using NorthernLink.BuildingBlocks.Infrastructure.EventBus;

namespace NorthernLink.BuildingBlocks.Infrastructure;

public static class BuildingBlocksServiceCollectionExtensions
{
    /// <summary>
    /// Registers the platform-wide building blocks: the in-process command/query dispatcher
    /// and the RabbitMQ integration event bus. Called once by the API host, before modules.
    /// </summary>
    public static IServiceCollection AddNorthernLinkBuildingBlocks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ISender, Sender>();

        var rabbitMqOptions = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
            ?? new RabbitMqOptions();
        services.AddSingleton(rabbitMqOptions);
        services.AddSingleton<RabbitMqIntegrationEventBus>();
        services.AddSingleton<IIntegrationEventBus>(sp => sp.GetRequiredService<RabbitMqIntegrationEventBus>());
        services.AddHostedService<RabbitMqInitializer>();

        return services;
    }
}
