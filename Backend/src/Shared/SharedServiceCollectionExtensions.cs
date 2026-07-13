using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.Persistence.Auditing;

namespace NorthernLink.Shared;

public static class SharedServiceCollectionExtensions
{
    /// <summary>
    /// Registers the platform-wide building blocks: the in-process command/query dispatcher,
    /// the RabbitMQ integration event bus, and the outbox plumbing every module's dispatcher
    /// shares. Called once by the API host, before modules.
    /// </summary>
    public static IServiceCollection AddNorthernLinkShared(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ISender, Sender>();

        var rabbitMqOptions = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
            ?? new RabbitMqOptions();
        services.AddSingleton(rabbitMqOptions);
        services.AddSingleton<RabbitMqIntegrationEventBus>();
        services.AddSingleton<IIntegrationEventBus>(sp => sp.GetRequiredService<RabbitMqIntegrationEventBus>());
        services.AddSingleton<IOutboxTransport>(sp => sp.GetRequiredService<RabbitMqIntegrationEventBus>());
        services.AddHostedService<RabbitMqInitializer>();

        var outboxOptions = configuration.GetSection(OutboxOptions.SectionName).Get<OutboxOptions>()
            ?? new OutboxOptions();
        services.AddSingleton(outboxOptions);

        return services;
    }
}
