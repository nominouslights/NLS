using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Schedules.Events;

/// <summary>Raised when a schedule template is activated or deactivated. Internal only — see <see cref="ScheduleTemplateCreatedDomainEvent"/>.</summary>
public sealed record ScheduleTemplateActivationChangedDomainEvent(Guid ScheduleTemplateId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
