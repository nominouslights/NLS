using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Schedules;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="TripsDbContext"/> (tenant-filtered).</summary>
internal sealed class ScheduleTemplateRepository(TripsDbContext context) : IScheduleTemplateRepository
{
    public void Add(ScheduleTemplate template) => context.ScheduleTemplates.Add(template);

    public Task<ScheduleTemplate?> GetByIdAsync(Guid templateId, CancellationToken cancellationToken = default) =>
        context.ScheduleTemplates.FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
