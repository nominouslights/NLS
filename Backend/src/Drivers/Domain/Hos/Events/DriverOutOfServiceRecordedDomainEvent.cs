using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos.Events;

/// <summary>
/// Raised when a compliance officer records an out-of-service declaration. Maps to
/// <c>drivers.driver-out-of-service-changed</c> (status <c>Active</c>); the handler that raised
/// it also opens the Severe <see cref="ComplianceFinding"/> in the same transaction.
/// </summary>
public sealed record DriverOutOfServiceRecordedDomainEvent(
    Guid RecordId,
    Guid DriverId,
    Guid TenantId,
    DateTimeOffset DeclaredAtUtc,
    DateTimeOffset? UntilUtc) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
