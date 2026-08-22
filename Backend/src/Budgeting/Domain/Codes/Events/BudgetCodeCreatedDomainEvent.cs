using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Codes.Events;

/// <summary>
/// Raised when a budget code is first created (always active).
/// <para>
/// <see cref="ActorId"/> is the authenticated user from the access token's <c>sub</c> claim, and
/// it rides the event rather than only the aggregate for a specific reason: the audit pipeline
/// serializes each event into <c>event_journal.payload</c>, so <c>payload-&gt;&gt;'actorId'</c>
/// answers "who did this" per journal row today. <c>event_journal.actor_id</c> exists as a column
/// but nothing writes it yet — when the platform-wide story wires <c>ModuleDbContext</c> to fill
/// it, this field can move out of the payload and these records can drop it.
/// </para>
/// </summary>
public sealed record BudgetCodeCreatedDomainEvent(
    Guid BudgetCodeId,
    Guid TenantId,
    string Code,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
