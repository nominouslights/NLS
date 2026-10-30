using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos.Events;

/// <summary>
/// Raised when a <see cref="HosException"/> comes into existence — declared by the driver or
/// dispatch, or approved outright by a compliance officer (a deferral). Maps to
/// <c>drivers.hos-exception-changed</c>, which carries the current status, so consumers upsert.
/// </summary>
public sealed record HosExceptionDeclaredDomainEvent(
    Guid ExceptionId,
    Guid DriverId,
    Guid TenantId,
    HosExceptionKind Kind) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
