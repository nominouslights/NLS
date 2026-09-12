using NorthernLink.Shared.Kernel;

namespace NorthernLink.Identity.Domain.Users.Events;

/// <summary>
/// Raised when a user edits their own profile — the first mutation <c>User</c> has ever had.
/// Carries the <em>new</em> values because <c>identity.event_journal.payload</c> is the audit
/// record, and "what did this change to" has to be answerable from the journal alone.
/// <para>
/// Deliberately no Email or Role: neither can change here, and
/// <c>IdentityIntegrationEventMapper</c> reads every field off the aggregate rather than off the
/// event, which is what guarantees the published snapshot is the stored truth.
/// </para>
/// </summary>
public sealed record UserProfileUpdatedDomainEvent(
    Guid UserId,
    Guid TenantId,
    string? FullName,
    string? JobTitle) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
