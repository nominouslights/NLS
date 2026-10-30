using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos.Events;

/// <summary>Raised when a compliance officer resolves an ELD malfunction report. Maps to <c>drivers.eld-malfunction-changed</c> (status <c>Resolved</c>).</summary>
public sealed record EldMalfunctionResolvedDomainEvent(
    Guid ReportId,
    Guid DriverId,
    Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
