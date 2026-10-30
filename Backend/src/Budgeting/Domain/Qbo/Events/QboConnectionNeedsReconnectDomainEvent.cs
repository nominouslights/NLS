using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Qbo.Events;

/// <summary>
/// Raised when an Active connection stops working and needs a person to run the OAuth flow
/// again. <paramref name="ErrorCode"/> is a short machine code (<c>invalid_grant</c>,
/// <c>token_undecryptable</c>), never a message from Intuit.
/// </summary>
public sealed record QboConnectionNeedsReconnectDomainEvent(
    Guid ConnectionId,
    Guid TenantId,
    string ErrorCode) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
