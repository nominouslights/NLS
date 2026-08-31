using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Schedules.Events;

/// <summary>
/// Raised by every exception mutation (add/update/remove) on a schedule template.
/// Internal only — it exists so the projection re-reads the aggregate and rewrites the
/// <c>rm_schedule_exceptions</c> rows; nothing crosses the module boundary.
/// </summary>
public sealed record ScheduleTemplateExceptionsChangedDomainEvent(Guid ScheduleTemplateId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
