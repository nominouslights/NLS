namespace NorthernLink.Notifications.Domain.Dispatches;

/// <summary>
/// One recipient's outcome within a dispatch, persisted as jsonb on the dispatch row (no
/// child table — outcomes are written once when the dispatch is recorded, never addressed
/// individually by SQL). <see cref="ErrorCode"/> is a stable machine code
/// (<c>Postmark.{code}</c> for per-message provider errors, <c>Postmark.Unauthorized</c> /
/// <c>Postmark.Unreachable</c> / <c>Postmark.Http{status}</c> for whole-request transport
/// failures); <see cref="ErrorMessage"/> is the human-readable detail shown to dispatchers.
/// </summary>
public sealed record DispatchRecipient
{
    public required string Email { get; init; }
    public required string PassengerName { get; init; }
    public DispatchRecipientStatus Status { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? PostmarkMessageId { get; init; }
}
