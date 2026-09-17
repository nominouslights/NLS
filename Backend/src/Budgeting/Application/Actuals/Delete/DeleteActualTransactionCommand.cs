using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Actuals.Delete;

/// <summary>
/// Removes a transaction that should never have been recorded — a duplicate entry, or one that
/// belongs to a different tenant's books entirely. Correcting a real transaction is
/// <c>UpdateActualTransactionCommand</c>; this is for rows that are not facts at all.
/// <para>
/// There is no ActorId: a deleted row has nowhere to record who deleted it. The audit pipeline
/// still writes a final snapshot plus the synthetic <c>aggregate-deleted</c> journal row, so the
/// transaction's last state survives — attributing the removal itself needs
/// <c>event_journal.actor_id</c>, which the platform-wide actor story fills in (the
/// <c>DeleteBudgetCodeCommand</c> reasoning, unchanged).
/// </para>
/// </summary>
public sealed record DeleteActualTransactionCommand(Guid TenantId, Guid TransactionId) : ICommand;
