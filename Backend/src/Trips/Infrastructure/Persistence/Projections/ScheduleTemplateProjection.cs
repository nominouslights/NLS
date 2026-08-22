using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Trips.Domain.Schedules;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Trips.Infrastructure.Persistence.Projections;

/// <summary>
/// Projects <see cref="ScheduleTemplate"/> into <c>trips.rm_schedule_templates</c>,
/// resolving the route name from the same-schema routes table (a display
/// denormalization — a renamed route is picked up the next time the template itself is
/// touched, or on a rebuild). See <see cref="TripProjection"/> for the worker/tenancy
/// notes.
/// </summary>
internal sealed class ScheduleTemplateProjection : IProjection<TripsDbContext>
{
    public string AggregateType { get; } = AuditNames.ForAggregate(typeof(ScheduleTemplate));

    public async Task ApplyAsync(TripsDbContext context, Guid aggregateId, CancellationToken cancellationToken)
    {
        var template = await context.ScheduleTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == aggregateId, cancellationToken);

        var row = await context.ScheduleTemplateReadModels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == aggregateId, cancellationToken);

        if (template is null)
        {
            if (row is not null)
            {
                context.ScheduleTemplateReadModels.Remove(row);
            }

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
    }

    public async Task RebuildAllAsync(TripsDbContext context, CancellationToken cancellationToken)
    {
        var templates = await context.ScheduleTemplates.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var rows = await context.ScheduleTemplateReadModels.IgnoreQueryFilters().ToListAsync(cancellationToken);

        var rowsById = rows.ToDictionary(row => row.Id);
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
        }

        foreach (var (id, row) in rowsById)
        {
            if (!seen.Contains(id))
            {
                context.ScheduleTemplateReadModels.Remove(row);
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
}
