using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NorthernLink.BuildingBlocks.Infrastructure.EventBus;

/// <summary>
/// Eagerly connects to RabbitMQ and declares the exchange at host startup. A down broker
/// logs a warning instead of failing the host — the bus reconnects lazily on first publish.
/// </summary>
public sealed class RabbitMqInitializer(
    RabbitMqIntegrationEventBus bus,
    ILogger<RabbitMqInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await bus.EnsureChannel(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "RabbitMQ is unreachable at startup — the host will continue and the event bus " +
                "will retry on first publish. Is docker compose up? ({Message})", ex.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
