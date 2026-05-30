using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Application.Community.Commands;

namespace ShuttleApi.Infrastructure.BackgroundJobs;

internal sealed class CutoffProcessorHostedService(
    IServiceProvider serviceProvider,
    ILogger<CutoffProcessorHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var result = await sender.Send(new ProcessCutoffCommand(), stoppingToken);

                if (result.OpenedForPayment > 0 || result.Released > 0 || result.TripsCancelled > 0)
                {
                    logger.LogInformation(
                        "Cutoff processor: {Opened} opened for payment, {Released} released, {Cancelled} trips cancelled.",
                        result.OpenedForPayment, result.Released, result.TripsCancelled);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error running cutoff processor.");
            }
        }
    }
}
