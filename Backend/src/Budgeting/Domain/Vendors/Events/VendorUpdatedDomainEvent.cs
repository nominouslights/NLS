using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Vendors.Events;

/// <summary>
/// Raised when a vendor's details actually change. An edit that changes nothing raises nothing —
/// see <see cref="Vendor.Update"/>. <see cref="ActorId"/> carries the authenticated user.
/// </summary>
public sealed record VendorUpdatedDomainEvent(Guid VendorId, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
