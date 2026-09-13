using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Allocations.Events;

/// <summary>
/// Raised when a budget period gets its first line against a code. Carries the code string and
/// the amount so a journal row reads on its own. <see cref="ActorId"/> carries the authenticated
/// user — see <c>BudgetCodeCreatedDomainEvent</c> for why it rides the event rather than only
/// the aggregate (it is what fills <c>event_journal.actor_id</c>'s role today).
/// </summary>
public sealed record BudgetAllocationCreatedDomainEvent(
    Guid AllocationId,
    Guid TenantId,
    Guid PeriodId,
    Guid BudgetCodeId,
    string Code,
    decimal AmountCad,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
