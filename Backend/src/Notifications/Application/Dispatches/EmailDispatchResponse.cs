namespace NorthernLink.Notifications.Application.Dispatches;

/// <summary>
/// The Notifications module's public representation of one send action — returned by the send
/// endpoints (whatever the outcome) and by the email-history query. Trip pickup dispatches
/// carry <paramref name="TripId"/>/<paramref name="TripNumber"/> and
/// <paramref name="TemplateId"/>/<paramref name="TemplateName"/>; client accruals dispatches
/// carry neither (all four null) and are anchored by <paramref name="ClientId"/> instead.
/// <paramref name="ServiceType"/> is the <c>NotificationServiceType</c> name as a string;
/// <paramref name="Status"/> is Sent, PartiallyFailed, or Failed.
/// </summary>
public sealed record EmailDispatchResponse(
    Guid Id,
    Guid? TripId,
    string? TripNumber,
    Guid? ManifestId,
    Guid? TemplateId,
    string? TemplateName,
    string ServiceType,
    Guid? ClientId,
    string Status,
    DateTimeOffset SentAtUtc,
    List<RecipientResult> Recipients);

/// <summary>
/// One recipient's outcome. <paramref name="Status"/> is Sent or Failed;
/// <paramref name="ErrorCode"/> is the stable machine code (<c>Postmark.*</c>) when failed.
/// </summary>
public sealed record RecipientResult(
    string Email,
    string PassengerName,
    string Status,
    string? ErrorCode,
    string? ErrorMessage,
    string? PostmarkMessageId);
