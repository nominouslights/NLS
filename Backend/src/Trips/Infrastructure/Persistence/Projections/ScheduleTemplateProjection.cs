using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Trips.Domain.Schedules;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Trips.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="ScheduleTemplate"/> into <c>trips.rm_schedule_templates</c> <b>and</b>
/// its exceptions into <c>trips.rm_schedule_exceptions</c> — one projection writing two
/// tables, on the <see cref="ShipmentProjection"/> model: the template's exception rows are
/// upserted and stale ones deleted on every event, so the read side can never drift from the
/// aggregate. The route name is resolved from the same-schema routes table (a display
/// denormalization — a renamed route is picked up the next time the template itself is
/// touched, or on a rebuild). See <see cref="TripProjection"/> for the worker/tenancy notes.
/// </summary>
internal sealed class ScheduleTemplateProjection : IProjection<TripsDbContext>
{
    public string AggregateType { get; } = AuditNames.ForAggregate(typeof(ScheduleTemplate));

    public async Task ApplyAsync(TripsDbContext context, Guid aggregateId, CancellationToken cancellationToken)
    {
        var template = await context.ScheduleTemplates
            .IgnoreQueryFilters()
            .Include(t => t.Exceptions)
            .FirstOrDefaultAsync(t => t.Id == aggregateId, cancellationToken);

        var row = await context.ScheduleTemplateReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == aggregateId, cancellationToken);

        var exceptionRows = await context.ScheduleExceptionReadModels
            .IgnoreQueryFilters()
            .Where(r => r.ScheduleTemplateId == aggregateId)
            .ToListAsync(cancellationToken);

        if (template is null)
        {
            if (row is not null)
            {
                context.ScheduleTemplateReadModels.Remove(row);
            }

            context.ScheduleExceptionReadModels.RemoveRange(exceptionRows);
            return;
        }

        var routeName = await ResolveRouteNameAsync(context, template.RouteId, cancellationToken);

        if (row is null)
        {
            row = new ScheduleTemplateReadModel();
            Map(template, routeName, row);
            context.ScheduleTemplateReadModels.Add(row);
        }
        else
        {
            Map(template, routeName, row);
        }

        SyncExceptions(context, template, exceptionRows);
    }

    public async Task RebuildAllAsync(TripsDbContext context, CancellationToken cancellationToken)
    {
        var templates = await context.ScheduleTemplates
            .IgnoreQueryFilters()
            .Include(t => t.Exceptions)
            .ToListAsync(cancellationToken);

        var rows = await context.ScheduleTemplateReadModels.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var exceptionRows = await context.ScheduleExceptionReadModels.IgnoreQueryFilters().ToListAsync(cancellationToken);

        var rowsById = rows.ToDictionary(row => row.Id);
        var exceptionsByTemplate = exceptionRows
            .GroupBy(r => r.ScheduleTemplateId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var seen = new HashSet<Guid>();

        foreach (var template in templates)
        {
            seen.Add(template.Id);
            var routeName = await ResolveRouteNameAsync(context, template.RouteId, cancellationToken);

            if (rowsById.TryGetValue(template.Id, out var existing))
            {
                Map(template, routeName, existing);
            }
            else
            {
                var fresh = new ScheduleTemplateReadModel();
                Map(template, routeName, fresh);
                context.ScheduleTemplateReadModels.Add(fresh);
            }

            SyncExceptions(
                context,
                template,
                exceptionsByTemplate.TryGetValue(template.Id, out var existingExceptions) ? existingExceptions : []);
        }

        foreach (var (id, row) in rowsById)
        {
            if (!seen.Contains(id))
            {
                context.ScheduleTemplateReadModels.Remove(row);
            }
        }

        foreach (var (templateId, orphans) in exceptionsByTemplate)
        {
            if (!seen.Contains(templateId))
            {
                context.ScheduleExceptionReadModels.RemoveRange(orphans);
            }
        }
    }

    private static async Task<string?> ResolveRouteNameAsync(
        TripsDbContext context,
        Guid routeId,
        CancellationToken cancellationToken)
    {
        var route = await context.Routes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == routeId, cancellationToken);

        return route?.Name;
    }

    /// <summary>
    /// Upserts this template's exception rows and deletes any that no longer exist — a
    /// removed special date has to disappear from the calendar immediately, or a dispatcher
    /// plans around an exception the aggregate no longer has.
    /// </summary>
    private static void SyncExceptions(
        TripsDbContext context,
        ScheduleTemplate template,
        List<ScheduleExceptionReadModel> existingRows)
    {
        var byId = existingRows.ToDictionary(r => r.Id);

        foreach (var exception in template.Exceptions)
        {
            if (byId.TryGetValue(exception.Id, out var existing))
            {
                MapException(template, exception, existing);
                byId.Remove(exception.Id);
            }
            else
            {
                var fresh = new ScheduleExceptionReadModel();
                MapException(template, exception, fresh);
                context.ScheduleExceptionReadModels.Add(fresh);
            }
        }

        context.ScheduleExceptionReadModels.RemoveRange(byId.Values);
    }

    private static void Map(ScheduleTemplate source, string? routeName, ScheduleTemplateReadModel row)
    {
        row.Id = source.Id;
        row.TenantId = source.TenantId;
        row.Name = source.Name;
        row.RouteId = source.RouteId;
        row.RouteName = routeName;
        row.ServiceType = source.ServiceType.ToString();
        row.ClientId = source.ClientId;
        row.ClientName = source.ClientName;
        row.RecurrenceKind = source.RecurrenceKind.ToString();
        row.DaysOfWeek = [.. source.DaysOfWeek.Select(day => day.ToString())];
        row.IntervalDays = source.IntervalDays;
        row.AnchorDate = source.AnchorDate;
        row.DaysOfMonth = [.. source.DaysOfMonth];
        row.DepartureTime = source.DepartureTime;
        row.ReturnDepartureTime = source.ReturnDepartureTime;
        row.ReturnNextDay = source.ReturnNextDay;
        row.SeatsCapacity = source.SeatsCapacity;
        row.SeatsMinimum = source.SeatsMinimum;
        row.DefaultVehicleUnit = source.DefaultVehicleUnit;
        row.DefaultDriverId = source.DefaultDriverId;
        row.GenerationHorizonDays = source.GenerationHorizonDays;
        row.CutoffNote = source.CutoffNote;
        row.Active = source.Active;
        row.CreatedAtUtc = source.CreatedAtUtc;
        row.UpdatedAtUtc = source.UpdatedAtUtc;
        row.Version = source.Version;
    }

    private static void MapException(
        ScheduleTemplate template,
        ScheduleException exception,
        ScheduleExceptionReadModel row)
    {
        row.Id = exception.Id;
        row.TenantId = exception.TenantId;
        row.ScheduleTemplateId = exception.ScheduleTemplateId;
        row.Date = exception.Date;
        row.Kind = exception.Kind.ToString();
        row.DepartureTime = exception.DepartureTime;
        row.ReturnDepartureTime = exception.ReturnDepartureTime;
        row.Note = exception.Note;
        row.CreatedAtUtc = exception.CreatedAtUtc;
        row.UpdatedAtUtc = exception.UpdatedAtUtc;
        row.Version = template.Version;
    }
}
