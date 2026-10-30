using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos.Events;

/// <summary>
/// Raised when a <see cref="DutyDay"/> accepts a new duty event — including the system-minted
/// on-duty switch at the personal-conveyance limit, which raises its own. Maps to
/// <c>drivers.duty-day-changed</c> (the whole day's effective timeline), so consumers upsert.
/// </summary>
public sealed record DutyEventAppendedDomainEvent(
    Guid DutyDayId,
    Guid DriverId,
    Guid TenantId,
    DateOnly LocalDate,
    Guid EventId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
