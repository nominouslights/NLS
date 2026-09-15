using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Periods.Events;

/// <summary>
/// Raised on every successful <see cref="BudgetPeriod.Transition"/>. Carries both ends of the
/// step so the journal answers "what changed" without a second row to diff against.
/// <see cref="ActorId"/> carries the authenticated user — see
/// <c>BudgetCodeCreatedDomainEvent</c> for why it rides the event rather than only the aggregate.
/// </summary>
public sealed record BudgetPeriodStateChangedDomainEvent(
    Guid PeriodId,
    Guid TenantId,
    PeriodState From,
    PeriodState To,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
