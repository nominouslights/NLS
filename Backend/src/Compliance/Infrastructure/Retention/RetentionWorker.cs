using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Application.Records.QueueForDisposalReview;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Compliance.Infrastructure.Retention;

/// <summary>
/// The nightly retention sweep (NL-SP-02 s. 11): at <see cref="RetentionOptions.RunAtLocalTime"/>
/// in the carrier's time zone, every tenant's retained records are recomputed and the ones whose
/// retention has passed — and that carry no hold — move to ReviewForDisposal. Nothing is ever
/// disposed here; a person does that from the review list.
/// <para>
/// Two tenancy modes, the <c>TripGenerationWorker</c> shape: the tenant enumeration spans tenants
/// on a system session (<see cref="IRetentionSweepSource"/>), then each tenant's sweep runs in its
/// own scope resolved <em>inside</em> <see cref="AmbientTenant.Push"/>, so the DbContext captures
/// that tenant and every write — records, journal, outbox — goes through the normal tenant-scoped
/// pipeline exactly as if a compliance officer had clicked "sweep". One tenant's failure is logged
/// and never stops the others or kills the host.
/// </para>
/// </summary>
internal sealed class RetentionWorker(
    IServiceScopeFactory scopeFactory,
    RetentionOptions options,
    ILogger<RetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(options.InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var timeZone = options.ResolveTimeZone();

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = RetentionSchedule.NextRunAfter(DateTimeOffset.UtcNow, options.RunAt, timeZone);
            logger.LogInformation("Retention sweep scheduled for {NextRunUtc:u} ({TimeZone} {RunAt})", nextRun, timeZone.Id, options.RunAt);

            try
            {
                await Task.Delay(nextRun - DateTimeOffset.UtcNow is { Ticks: > 0 } wait ? wait : TimeSpan.Zero, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await OnceAsync(RetentionSchedule.TodayIn(DateTimeOffset.UtcNow, timeZone), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Retention sweep failed; retrying at the next scheduled run");
            }
        }
    }

    /// <summary>One full sweep over every tenant with retained records, as of <paramref name="today"/>. Exposed for tests.</summary>
    internal async Task<int> OnceAsync(DateOnly today, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> tenants;
        using (var scope = scopeFactory.CreateScope())
        {
            tenants = await scope.ServiceProvider.GetRequiredService<IRetentionSweepSource>()
                .ListTenantsWithRetainedRecordsAsync(cancellationToken);
        }

        var queuedTotal = 0;
        foreach (var tenantId in tenants)
        {
            try
            {
                queuedTotal += await SweepTenantAsync(tenantId, today, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Retention sweep for tenant {TenantId} failed; skipping", tenantId);
            }
        }

        return queuedTotal;
    }

    private async Task<int> SweepTenantAsync(Guid tenantId, DateOnly today, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        using (AmbientTenant.Push(tenantId))
        {
            // Resolved inside the push so the handler's DbContext captures the tenant (query
            // filters, stamping) and the RLS session variable is set at connection open.
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.Send(new QueueForDisposalReviewCommand(tenantId, today), cancellationToken);
            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Retention sweep for tenant {TenantId} refused: {ErrorCode} — {ErrorMessage}",
                    tenantId, result.Error.Code, result.Error.Message);
                return 0;
            }

            if (result.Value > 0)
            {
                logger.LogInformation(
                    "Retention sweep queued {Count} record(s) for disposal review in tenant {TenantId}", result.Value, tenantId);
            }

            return result.Value;
        }
    }
}
