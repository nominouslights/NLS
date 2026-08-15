using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Periods.Events;

/// <summary>Raised when a budget period is first created (always in Draft).</summary>
public sealed record BudgetPeriodCreatedDomainEvent(Guid PeriodId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
