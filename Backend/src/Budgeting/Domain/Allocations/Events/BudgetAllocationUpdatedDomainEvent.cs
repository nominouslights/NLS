using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Allocations.Events;

/// <summary>
/// Raised when a budget item is rewritten (a re-submit with the same values raises it too — see
/// <c>BudgetAllocation.Update</c>). Carries the code id and string because an update may move the
/// item to another code, plus the title and amount so the journal row reads on its own.
/// <see cref="ActorId"/> carries the authenticated user — see <c>BudgetCodeCreatedDomainEvent</c>
/// for why it rides the event.
/// </summary>
public sealed record BudgetAllocationUpdatedDomainEvent(
    Guid AllocationId,
    Guid BudgetCodeId,
    string Code,
    string Title,
    decimal AmountCad,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
