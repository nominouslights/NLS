using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Schedules.GenerateTrips;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Infrastructure.Persistence;

namespace NorthernLink.Trips.Infrastructure.Generation;

/// <summary>
/// Materializes upcoming trips from active schedule templates. Each pass has two
/// tenancy modes, mirroring <c>ProjectionWorker</c>: the template enumeration spans
/// tenants on a pinned connection opted into the tables' system RLS policy
/// (<c>app.is_system</c>), then each template is expanded in its own scope under the
/// template's tenant pushed as the ambient tenant — so the trips (and their audit
/// journal/outbox rows) are written through the normal tenant-scoped pipeline, exactly
/// as if a dispatcher had created them. One transaction (SaveChanges) per template;
/// idempotency comes from <c>TripGenerator</c> skipping already-materialized occurrence
/// keys, backstopped by the unique index on (tenant, template, service date, direction)
/// — a concurrent duplicate simply fails that template's save and is retried next pass.
/// Failures are logged and never kill the host.
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
            catch (DbUpdateException exception)
            {
                // Unique-index race with a concurrent pass — the occurrence already
                // exists; this template converges on the next pass.
                logger.LogWarning(
                    exception,
                    "Trip generation for template {TemplateId} hit an existing occurrence; will reconcile next pass",
                    template.TemplateId);
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
            // Resolved inside the push so the context captures the tenant (query filters,
            // stamping) and the RLS session variable is set at connection open.
            var context = scope.ServiceProvider.GetRequiredService<TripsDbContext>();
            var tripNumberGenerator = scope.ServiceProvider.GetRequiredService<ITripNumberGenerator>();

            var template = await context.ScheduleTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);
            if (template is null || !template.Active)
            {
                return;
            }

            var route = await context.Routes
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == template.RouteId, cancellationToken);
            if (route is null)
            {
                logger.LogWarning(
                    "Template {TemplateId} references missing route {RouteId}; skipping generation",
                    template.Id, template.RouteId);
                return;
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var horizonEnd = today.AddDays(template.GenerationHorizonDays);

            var existing = await context.Trips
                .AsNoTracking()
                .Where(t => t.ScheduleTemplateId == template.Id
                    && t.ServiceDate >= today
                    && t.ServiceDate < horizonEnd
                    && t.Direction != null)
                .Select(t => new { t.ServiceDate, t.Direction })
                .ToListAsync(cancellationToken);

            var existingKeys = existing
                .Select(t => (t.ServiceDate, t.Direction!.Value))
                .ToHashSet();

            var drafts = TripGenerator.Generate(template, existingKeys, today);
            if (drafts.Count == 0)
            {
                return;
            }

            // Default driver prefill — only when the driver is still Active in the replica.
            Guid? driverId = null;
            string? driverName = null;
            if (template.DefaultDriverId is { } defaultDriverId)
            {
                var driver = await context.DriverLookups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.DriverId == defaultDriverId, cancellationToken);
                if (driver is { IsActive: true })
                {
                    driverId = driver.DriverId;
                    driverName = driver.Name;
                }
            }

            var outboundStops = route.Stops.OrderBy(s => s.Order).ToList();
            var returnStops = outboundStops
                .AsEnumerable()
                .Reverse()
                .Select((stop, index) => new RouteStop { Name = stop.Name, Order = index })
                .ToList();

            var added = 0;
            foreach (var draft in drafts)
            {
                var outbound = draft.Direction == TripDirection.Outbound;
                var tripNumber = await tripNumberGenerator.NextAsync(tenantId, cancellationToken);

                var tripResult = Trip.Schedule(
                    tenantId,
                    tripNumber,
                    draft.ServiceDate,
                    draft.DepartureTime,
                    draft.DepartureTime.Add(route.EstimatedDuration),
                    template.ServiceType,
                    route.Id,
                    route.Name,
                    outbound ? route.Origin : route.Destination,
                    outbound ? route.Destination : route.Origin,
                    outbound ? outboundStops : returnStops,
                    route.DistanceKm,
                    template.Id,
                    draft.RoundTripKey,
                    draft.Direction,
                    isEmptyLeg: false,
                    template.ClientId,
                    template.ClientName,
                    poNumber: null,
                    driverId,
                    driverName,
                    vehicleId: null,
                    template.DefaultVehicleUnit,
                    template.SeatsCapacity,
                    template.SeatsMinimum);

                if (tripResult.IsFailure)
                {
                    logger.LogWarning(
                        "Template {TemplateId}: draft for {ServiceDate}/{Direction} rejected: {Error}",
                        template.Id, draft.ServiceDate, draft.Direction, tripResult.Error.Code);
                    continue;
                }

                context.Trips.Add(tripResult.Value);
                added++;
            }

            if (added > 0)
            {
                // Per-template transaction: all of this template's new legs (plus their
                // journal/snapshot/outbox rows) commit together.
                await context.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Generated {Count} trip(s) from template {TemplateId} ({TemplateName}) for tenant {TenantId}",
                    added, template.Id, template.Name, tenantId);
            }
        }
    }

    private sealed record TemplateKey(Guid TemplateId, Guid TenantId);
}
