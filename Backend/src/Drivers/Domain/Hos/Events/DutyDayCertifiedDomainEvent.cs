using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos.Events;

/// <summary>
/// Raised when the driver certifies a <see cref="DutyDay"/> (s.84). Maps to
/// <c>drivers.duty-day-changed</c> with <c>Certified = true</c>.
/// </summary>
public sealed record DutyDayCertifiedDomainEvent(
    Guid DutyDayId,
    Guid DriverId,
    Guid TenantId,
    DateOnly LocalDate,
    Guid CertifiedByUserId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
