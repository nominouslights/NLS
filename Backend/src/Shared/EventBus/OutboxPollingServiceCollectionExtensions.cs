using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Persistence;

namespace NorthernLink.Shared.EventBus;

public static class OutboxPollingServiceCollectionExtensions
{
    /// <summary>
    /// Declares a module as a storing/projecting integration-event consumer: one hosted
    /// <see cref="OutboxPollingConsumer{TDbContext}"/> that polls the producer modules'
    /// outbox tables (schemas derived from the subscribed events' routing keys) and runs
    /// the registered handlers in-process. Called from the module's DI extension; modules
    /// that consume nothing call nothing. Chain-reaction events that must trigger commands
    /// in another module use RabbitMQ instead — see <see cref="BusPublicationRegistry"/>.
    ///
    /// Subscribing to an event type replays that routing key's ENTIRE outbox history
    /// through the handler on first poll (rows start Pending) — a feature for building
    /// replicas, but it means every handler wired here must be idempotent and
    /// history-tolerant, not just tolerant of fresh events.
    /// </summary>
    public static IServiceCollection AddOutboxPollingConsumer<TDbContext>(
        this IServiceCollection services,
        string moduleName,
        Action<IntegrationEventSubscriptionsBuilder> configure)
        where TDbContext : ModuleDbContext
    {
        var subscriptions = new IntegrationEventSubscriptions(moduleName);
        configure(new IntegrationEventSubscriptionsBuilder(services, subscriptions));

        services.AddSingleton<IHostedService>(sp => new OutboxPollingConsumer<TDbContext>(
            sp.GetRequiredService<IServiceScopeFactory>(),
            subscriptions,
            sp.GetRequiredService<OutboxPollingOptions>(),
            sp.GetRequiredService<ILogger<OutboxPollingConsumer<TDbContext>>>()));

        return services;
    }
}
