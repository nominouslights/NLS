using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Allocations.Remove;

/// <summary>
/// Takes one budget code's line out of a period's plan. Addressed by (period, code) like the
/// set, because that pair is the identity a planner reaches a line by.
/// <para>
/// There is no ActorId: a deleted row has nowhere to record who deleted it. The audit pipeline
/// still writes a final snapshot plus the synthetic <c>aggregate-deleted</c> journal row, so the
/// line's last state survives — attributing the removal itself needs
/// <c>event_journal.actor_id</c>, which the platform-wide actor story fills in (the
/// <c>DeleteBudgetCodeCommand</c> reasoning, unchanged).
/// </para>
/// </summary>
public sealed record RemoveBudgetAllocationCommand(Guid TenantId, Guid PeriodId, Guid BudgetCodeId) : ICommand;
