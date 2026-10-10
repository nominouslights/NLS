using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Vendors.Events;

/// <summary>
/// Raised when a vendor is first added to the tenant's register (always active).
/// <see cref="ActorId"/> rides the event for the <c>BudgetCodeCreatedDomainEvent</c> reason: the
/// audit pipeline serializes it into <c>event_journal.payload</c>, which is where "who did this"
/// is answered per journal row today.
/// </summary>
public sealed record VendorCreatedDomainEvent(
    Guid VendorId,
    Guid TenantId,
    string Name,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
