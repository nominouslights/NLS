using NorthernLink.Trips.Domain.Schedules;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Write-side persistence for the ScheduleTemplate aggregate.
/// Implementations are tenant-scoped (EF global query filter + Postgres RLS).
/// </summary>
public interface IScheduleTemplateRepository
{
    void Add(ScheduleTemplate template);

    Task<ScheduleTemplate?> GetByIdAsync(Guid templateId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
