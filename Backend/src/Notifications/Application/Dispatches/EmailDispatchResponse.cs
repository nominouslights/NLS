namespace NorthernLink.Notifications.Application.Dispatches;

/// <summary>
/// The Notifications module's public representation of one send action against a trip —
/// returned by the send endpoint (whatever the outcome) and by the trip history query.
/// <paramref name="ServiceType"/> is the <c>NotificationServiceType</c> name as a string;
/// <paramref name="Status"/> is Sent, PartiallyFailed, or Failed.
/// </summary>
public sealed record EmailDispatchResponse(
    Guid Id,
    Guid TripId,
    string TripNumber,
    Guid? ManifestId,
    Guid TemplateId,
    string TemplateName,
    string ServiceType,
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
