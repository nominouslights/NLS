using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos.Events;

/// <summary>
/// Raised when a <see cref="HosException"/> is acknowledged, rejected or withdrawn. Maps to
/// <c>drivers.hos-exception-changed</c> — a replica reading <c>Rejected</c> or <c>Withdrawn</c>
/// stops applying the exception at once.
/// </summary>
public sealed record HosExceptionStatusChangedDomainEvent(
    Guid ExceptionId,
    Guid DriverId,
    Guid TenantId,
    HosExceptionStatus Status) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
