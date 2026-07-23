using NorthernLink.Trips.Application.Schedules;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Read side for schedule template queries — returns response DTOs from
/// <c>rm_schedule_templates</c>. Implementations are tenant-scoped (EF global query
/// filter + Postgres RLS).
/// </summary>
public interface IScheduleTemplateReadService
{
    Task<IReadOnlyList<ScheduleTemplateResponse>> GetTemplatesAsync(CancellationToken cancellationToken = default);
}
