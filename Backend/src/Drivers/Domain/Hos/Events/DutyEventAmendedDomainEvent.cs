using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos.Events;

/// <summary>
/// Raised when an event on a <see cref="DutyDay"/> is amended: the original is voided and kept,
/// the replacement appended with the reason. A certified day becomes "recertification
/// required". Maps to <c>drivers.duty-day-changed</c>.
/// </summary>
public sealed record DutyEventAmendedDomainEvent(
    Guid DutyDayId,
    Guid DriverId,
    Guid TenantId,
    DateOnly LocalDate,
    Guid OriginalEventId,
    Guid ReplacementEventId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
