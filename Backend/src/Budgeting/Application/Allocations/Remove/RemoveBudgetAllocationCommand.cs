using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Allocations.Remove;

/// <summary>
/// Takes one budget item out of a period's plan, addressed by (period, item id) — with many items
/// per code, the code no longer identifies one.
/// <para>
/// There is no ActorId: a deleted row has nowhere to record who deleted it. The audit pipeline
/// still writes a final snapshot plus the synthetic <c>aggregate-deleted</c> journal row, so the
/// item's last state survives — attributing the removal itself needs
/// <c>event_journal.actor_id</c>, which the platform-wide actor story fills in (the
/// <c>DeleteBudgetCodeCommand</c> reasoning, unchanged).
/// </para>
/// </summary>
public sealed record RemoveBudgetAllocationCommand(Guid TenantId, Guid PeriodId, Guid AllocationId) : ICommand;
