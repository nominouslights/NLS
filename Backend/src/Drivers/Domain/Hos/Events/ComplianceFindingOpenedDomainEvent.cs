using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos.Events;

/// <summary>Raised when a <see cref="ComplianceFinding"/> is opened. Internal to Drivers (no integration event) — findings feed the audit, not other modules.</summary>
public sealed record ComplianceFindingOpenedDomainEvent(
    Guid FindingId,
    Guid DriverId,
    Guid TenantId,
    string Rule,
    FindingSeverity Severity) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
