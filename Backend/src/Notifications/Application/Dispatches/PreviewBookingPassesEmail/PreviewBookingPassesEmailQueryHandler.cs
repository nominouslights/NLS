using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Dispatches.SendBookingPassesEmail;
using NorthernLink.Notifications.Domain.Dispatches;

namespace NorthernLink.Notifications.Application.Dispatches.PreviewBookingPassesEmail;

/// <summary>
/// Handles <see cref="PreviewBookingPassesEmailQuery"/>: gates the booking anchor, the
/// travellers and the recipients exactly like the send (shared
/// <see cref="BookingPassesRecipientGate"/>) → composes via the shared
/// <see cref="BookingPassesEmailComposer"/>. Reads only — no dispatch is recorded, and
/// <c>IEmailSender</c> is deliberately not injected, so this handler is structurally unable
/// to send.
/// </summary>
public sealed class PreviewBookingPassesEmailQueryHandler
    : IQueryHandler<PreviewBookingPassesEmailQuery, BookingPassesEmailPreviewResponse>
{
    public Task<Result<BookingPassesEmailPreviewResponse>> Handle(
        PreviewBookingPassesEmailQuery query,
        CancellationToken cancellationToken)
    {
        // Same booking anchor gate as the send, so a doomed send fails at preview time too.
        if (query.BookingId == Guid.Empty || string.IsNullOrWhiteSpace(query.BookingReference))
        {
            return Task.FromResult(
                Result.Failure<BookingPassesEmailPreviewResponse>(EmailDispatchErrors.BookingRequired));
        }

        var recipientsResult = BookingPassesRecipientGate.Normalize(query.Sheet, query.Recipients);
        if (recipientsResult.IsFailure)
        {
            return Task.FromResult(
                Result.Failure<BookingPassesEmailPreviewResponse>(recipientsResult.Error));
        }

        var recipients = recipientsResult.Value;
        var composition = BookingPassesEmailComposer.Compose(query.Sheet);

        var response = new BookingPassesEmailPreviewResponse(
            composition.Subject,
            composition.HtmlBody,
            composition.TextBody,
            recipients.Count,
            recipients.Select(recipient => recipient.Email).ToList());

        return Task.FromResult(Result.Success(response));
    }
}
