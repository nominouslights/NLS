using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Allocations.Events;

/// <summary>
/// Raised when a line's amount or justification is rewritten (a re-submit with the same values
/// raises it too — see <c>BudgetAllocation.Update</c>). The code string never changes, so it is
/// not repeated here. <see cref="ActorId"/> carries the authenticated user — see
/// <c>BudgetCodeCreatedDomainEvent</c> for why it rides the event.
/// </summary>
public sealed record BudgetAllocationUpdatedDomainEvent(
    Guid AllocationId,
    decimal AmountCad,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
