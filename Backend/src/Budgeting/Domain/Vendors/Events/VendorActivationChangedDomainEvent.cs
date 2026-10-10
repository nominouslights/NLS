using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Vendors.Events;

/// <summary>
/// Raised when a vendor is retired or brought back. Retiring is a flag flip, never a delete:
/// whatever already names the vendor must keep resolving to it.
/// </summary>
public sealed record VendorActivationChangedDomainEvent(
    Guid VendorId,
    bool IsActive,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
