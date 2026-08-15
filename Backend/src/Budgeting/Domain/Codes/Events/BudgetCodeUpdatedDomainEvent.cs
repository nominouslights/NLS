using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Codes.Events;

/// <summary>
/// Raised when a budget code's descriptive details change. The code string never does.
/// <see cref="ActorId"/> carries the authenticated user — see
/// <see cref="BudgetCodeCreatedDomainEvent"/> for why it rides the event.
/// </summary>
public sealed record BudgetCodeUpdatedDomainEvent(Guid BudgetCodeId, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
