using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Actuals.Events;

/// <summary>
/// Raised when a recorded transaction is corrected (a re-submit with the same values raises it
/// too — see <c>ActualTransaction.Update</c>). Unlike the allocation's updated event this one
/// repeats the code id and string: a correction may re-point the transaction at a different
/// budget code, and a journal row that only said "the amount is now X" would leave a miscoding
/// fix invisible. <see cref="OccurredOn"/> is here for the same reason — moving the date is how a
/// transaction moves between periods. <see cref="ActualTransactionSource"/> is not, because it
/// never changes. <see cref="ActorId"/> carries the authenticated user — see
/// <c>BudgetCodeCreatedDomainEvent</c> for why it rides the event.
/// </summary>
public sealed record ActualTransactionUpdatedDomainEvent(
    Guid TransactionId,
    Guid BudgetCodeId,
    string Code,
    decimal AmountCad,
    DateOnly OccurredOn,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
