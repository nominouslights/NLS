using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.CostCentres.Events;

/// <summary>
/// Raised when a cost centre is added to the tenant's register (always active).
/// <see cref="ActorId"/> rides the event for the reason given on
/// <c>BudgetCodeCreatedDomainEvent</c>: <c>event_journal.payload-&gt;&gt;'actorId'</c> is how the
/// journal answers "who did this" until <c>event_journal.actor_id</c> is filled platform-wide.
/// </summary>
public sealed record CostCentreCreatedDomainEvent(
    Guid CostCentreId,
    Guid TenantId,
    string Code,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
