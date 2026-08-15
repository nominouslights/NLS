using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Codes.Events;

/// <summary>
/// Raised when a budget code is retired or brought back. Retiring is a flag flip, never a
/// delete: allocations and actuals already tagged with the code must keep resolving.
/// <see cref="ActorId"/> carries the authenticated user — see
/// <see cref="BudgetCodeCreatedDomainEvent"/> for why it rides the event.
/// </summary>
public sealed record BudgetCodeActivationChangedDomainEvent(
    Guid BudgetCodeId,
    bool IsActive,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
