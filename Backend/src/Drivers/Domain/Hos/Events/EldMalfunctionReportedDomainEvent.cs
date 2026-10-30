using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos.Events;

/// <summary>Raised when an ELD malfunction is reported. Maps to <c>drivers.eld-malfunction-changed</c> (status <c>Open</c>).</summary>
public sealed record EldMalfunctionReportedDomainEvent(
    Guid ReportId,
    Guid DriverId,
    Guid TenantId,
    string Code) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
