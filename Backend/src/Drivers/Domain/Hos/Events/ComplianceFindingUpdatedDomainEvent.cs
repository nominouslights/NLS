using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos.Events;

/// <summary>Raised when a <see cref="ComplianceFinding"/>'s status or follow-up note changes. Internal to Drivers.</summary>
public sealed record ComplianceFindingUpdatedDomainEvent(
    Guid FindingId,
    Guid DriverId,
    Guid TenantId,
    FindingStatus Status) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
