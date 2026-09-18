using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Actuals.Events;

/// <summary>
/// Raised when a spend (or receipt) is recorded against a budget code. Carries the code string,
/// the amount and the date so a journal row reads on its own — the date especially, because
/// nothing else on the row says which period the figure landed in.
/// <see cref="Source"/> rides along so the journal distinguishes a figure someone typed from one
/// an import produced without having to join anything. <see cref="ActorId"/> carries the
/// authenticated user — see <c>BudgetCodeCreatedDomainEvent</c> for why it rides the event rather
/// than only the aggregate (it is what fills <c>event_journal.actor_id</c>'s role today).
/// </summary>
public sealed record ActualTransactionCreatedDomainEvent(
    Guid TransactionId,
    Guid TenantId,
    Guid BudgetCodeId,
    string Code,
    decimal AmountCad,
    DateOnly OccurredOn,
    ActualTransactionSource Source,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
