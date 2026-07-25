using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Schedules.Events;

/// <summary>
/// Raised when a schedule template is created. Internal only (maps to null in
/// <c>TripsIntegrationEventMapper</c>); its purpose is to give the aggregate a journal
/// row so <c>ScheduleTemplateProjection</c> populates <c>rm_schedule_templates</c>.
/// </summary>
public sealed record ScheduleTemplateCreatedDomainEvent(Guid ScheduleTemplateId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
