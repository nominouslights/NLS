using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Schedules.GenerateTrips;
using NorthernLink.Trips.Domain.Schedules;
using NorthernLink.Trips.Infrastructure.Persistence;

namespace NorthernLink.Trips.Infrastructure.Generation;

/// <summary>
/// Keeps every active schedule template's own horizon materialized into trips. Each pass
/// has two tenancy modes, mirroring <c>ProjectionWorker</c>: the template enumeration spans
/// tenants on a pinned connection opted into the tables' system RLS policy
/// (<c>app.is_system</c>), then each template is expanded in its own scope under the
/// template's tenant pushed as the ambient tenant — so the trips (and their audit
/// journal/outbox rows) are written through the normal tenant-scoped pipeline, exactly
/// as if a dispatcher had created them. The expansion itself is
/// <see cref="ScheduleTripMaterializer"/>, the same code path the on-demand generate
/// endpoint drives to a dispatcher-chosen date; here it runs to the template's horizon.
/// One transaction (SaveChanges) per template; idempotency comes from the materializer
/// skipping already-materialized occurrence keys, backstopped by the unique index on
/// (tenant, template, service date, direction) — a concurrent duplicate simply fails that
/// template's save and is retried next pass. Failures are logged and never kill the host.
/// </summary>
internal sealed class TripGenerationWorker(
    IServiceScopeFactory scopeFactory,
    TripGenerationOptions options,
    ILogger<TripGenerationWorker> logger) : BackgroundService
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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Trip generation pass failed; retrying next interval");
            }

            try
            {
                await Task.Delay(options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>One full pass over every active template. Exposed for tests.</summary>
    internal async Task GenerateOnceAsync(CancellationToken cancellationToken)
    {
        List<TemplateKey> templates;

        // System session: enumerate active templates across every tenant.
        using (var scope = scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TripsDbContext>();
            await context.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    "SELECT set_config('app.is_system', 'true', false);", cancellationToken);

                var activeTemplates = await context.ScheduleTemplates
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(t => t.Active)
                    .Select(t => new { t.Id, t.TenantId })
                    .ToListAsync(cancellationToken);

                templates = activeTemplates
                    .Select(t => new TemplateKey(t.Id, t.TenantId))
                    .ToList();
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
        }

        foreach (var template in templates)
        {
            try
            {
                await GenerateForTemplateAsync(template.TemplateId, template.TenantId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Trip generation for template {TemplateId} (tenant {TenantId}) failed; skipping",
                    template.TemplateId, template.TenantId);
            }
        }
    }

    private async Task GenerateForTemplateAsync(Guid templateId, Guid tenantId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        using (AmbientTenant.Push(tenantId))
        {
            // Resolved inside the push so the materializer's DbContext captures the tenant
            // (query filters, stamping) and the RLS session variable is set at connection open.
            var materializer = scope.ServiceProvider.GetRequiredService<ScheduleTripMaterializer>();

            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

            // through: null ⇒ the template's own horizon. A template that can't materialize
            // (no default driver/unit, or they are inactive) stays paused with a warning until
            // a dispatcher fixes it — the same guard the on-demand endpoint shows verbatim.
            var plan = await materializer.PlanAsync(templateId, today, through: null, cancellationToken);
            if (plan.IsFailure)
            {
                // NotFound/TemplateInactive: deactivated between enumeration and now — silent.
                if (plan.Error != ScheduleTemplateErrors.NotFound
                    && plan.Error != ScheduleTemplateErrors.TemplateInactive)
                {
                    logger.LogWarning(
                        "Template {TemplateId} skipped: {ErrorCode} — {ErrorMessage}",
                        templateId, plan.Error.Code, plan.Error.Message);
                }

                return;
            }

            var applied = await materializer.ApplyAsync(tenantId, plan.Value, cancellationToken);
            if (applied.IsFailure)
            {
                // GenerationConflict is the unique-index race with a concurrent run — the
                // occurrence already exists; this template converges on the next pass. Any
                // other failure is a rejected draft, which skips the whole template.
                logger.LogWarning(
                    "Trip generation for template {TemplateId} failed: {ErrorCode} — {ErrorMessage}; will reconcile next pass",
                    templateId, applied.Error.Code, applied.Error.Message);
                return;
            }

            if (applied.Value.TripCount > 0)
            {
                logger.LogInformation(
                    "Generated {Count} trip(s) from template {TemplateId} ({TemplateName}) for tenant {TenantId}",
                    applied.Value.TripCount, templateId, plan.Value.Template.Name, tenantId);
            }
        }
    }

    private sealed record TemplateKey(Guid TemplateId, Guid TenantId);
}
