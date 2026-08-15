using NorthernLink.Notifications.Domain.Dispatches;

namespace NorthernLink.Notifications.Application.Dispatches;

/// <summary>
/// Maps the EmailDispatch aggregate to its public response — used by the send handler
/// (fresh sends and idempotent replays both return the aggregate's state directly, no
/// read-model round trip).
/// </summary>
public static class EmailDispatchResponseMapper
{
    /// <summary>Builds the public response from the aggregate.</summary>
    public static EmailDispatchResponse ToResponse(EmailDispatch dispatch) => new(
        dispatch.Id,
        dispatch.TripId,
        dispatch.TripNumber,
        dispatch.ManifestId,
        dispatch.TemplateId,
        dispatch.TemplateName,
        dispatch.ServiceType.ToString(),
        dispatch.ClientId,
        dispatch.Status.ToString(),
        dispatch.SentAtUtc,
        dispatch.Recipients
            .Select(r => new RecipientResult(
                r.Email, r.PassengerName, r.Status.ToString(), r.ErrorCode, r.ErrorMessage, r.PostmarkMessageId))
            .ToList());
}
