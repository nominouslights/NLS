using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Codes.Delete;

/// <summary>
/// Permanently removes a budget code. The narrow escape hatch retirement does not cover — a code
/// created in error that nothing has ever referenced. Anything that has been used is retired
/// instead, and the handler refuses the delete rather than leaving orphaned references behind.
/// <para>
/// There is no ActorId: a deleted row has nowhere to record who deleted it. The audit pipeline
/// still writes a final aggregate snapshot plus the synthetic <c>aggregate-deleted</c> journal
/// row, so the code's pre-delete state survives — but attributing the deletion itself needs
/// <c>event_journal.actor_id</c>, which the platform-wide actor story fills in.
/// </para>
/// </summary>
public sealed record DeleteBudgetCodeCommand(Guid TenantId, Guid BudgetCodeId) : ICommand;
