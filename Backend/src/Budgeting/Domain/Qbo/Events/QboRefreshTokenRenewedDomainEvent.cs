using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Qbo.Events;

/// <summary>
/// Raised when a token refresh pushed the refresh token's expiry out by a day or more, so the
/// console's "reconnect by" date follows the rolling expiry. Throttled to at most about one a day
/// (see <see cref="QboConnection.NoteRefreshTokenExpiry"/>), which keeps the audit trail quiet.
/// </summary>
public sealed record QboRefreshTokenRenewedDomainEvent(
    Guid ConnectionId,
    Guid TenantId,
    DateTimeOffset RefreshTokenExpiresAtUtc) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
