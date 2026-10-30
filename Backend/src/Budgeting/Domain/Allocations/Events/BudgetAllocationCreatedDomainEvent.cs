using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Allocations.Events;

/// <summary>
/// Raised when a budget item is added to a period (by create or by copy). Carries the code
/// string, the title and the amount so a journal row reads on its own; the rest of the item is in
/// the journal's aggregate snapshot, and the projection maps from the aggregate itself, not from
/// this payload. <see cref="ActorId"/> carries the authenticated user — see
/// <c>BudgetCodeCreatedDomainEvent</c> for why it rides the event rather than only the aggregate.
/// </summary>
public sealed record BudgetAllocationCreatedDomainEvent(
    Guid AllocationId,
    Guid TenantId,
    Guid PeriodId,
    Guid BudgetCodeId,
    string Code,
    string Title,
    decimal AmountCad,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
