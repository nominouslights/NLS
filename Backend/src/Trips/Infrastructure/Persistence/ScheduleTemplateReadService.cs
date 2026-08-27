using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Schedules;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>Read side — queries <c>trips.rm_schedule_templates</c> and maps to the public contract.</summary>
internal sealed class ScheduleTemplateReadService(TripsDbContext context) : IScheduleTemplateReadService
{
    public async Task<IReadOnlyList<ScheduleTemplateResponse>> GetTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        var templates = await context.ScheduleTemplateReadModels
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return templates.Select(t => new ScheduleTemplateResponse(
            t.Id,
            t.Name,
            t.RouteId,
            t.RouteName,
            t.ServiceType,
            t.ClientId,
            t.ClientName,
            t.RecurrenceKind,
            t.DaysOfWeek,
            t.IntervalDays,
            t.AnchorDate?.ToString("yyyy-MM-dd"),
            t.DaysOfMonth,
            t.DepartureTime,
            t.ReturnDepartureTime,
            t.ReturnNextDay,
            t.SeatsCapacity,
            t.SeatsMinimum,
            t.DefaultVehicleUnit,
            t.DefaultDriverId,
            t.GenerationHorizonDays,
            t.CutoffNote,
            t.Active,
            t.CreatedAtUtc,
            t.UpdatedAtUtc)).ToList();
    }
}
