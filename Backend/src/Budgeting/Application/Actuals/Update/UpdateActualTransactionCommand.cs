using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Actuals.Update;

/// <summary>
/// Corrects a recorded transaction: which budget code the spend belongs to, how much, when, and
/// what it was for. Addressed by the transaction's own id — unlike an allocation line, two spends
/// on the same code on the same day are two ordinary rows, so there is no pair to key on.
/// <para>
/// <paramref name="BudgetCodeId"/> is part of the correction on purpose. A miscoded expense is
/// the single most common thing a bookkeeper has to fix, and the alternative — delete and
/// re-enter — would throw away the row's history to change one field. The aggregate re-snapshots
/// the code string alongside the id so the two can never drift.
/// </para>
/// <para>
/// Source and external reference are not correctable: they say where the row came from, not what
/// it claims. <paramref name="ActorId"/> comes from the endpoint's <c>ICurrentActor</c> — the
/// signed token's <c>sub</c> claim — never from the request body.
/// </para>
/// </summary>
public sealed record UpdateActualTransactionCommand(
    Guid TenantId,
    Guid TransactionId,
    Guid BudgetCodeId,
    decimal? AmountCad,
    DateOnly? OccurredOn,
    string? Description,
    Guid? ActorId) : ICommand;
