using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Domain.Dispatches;

namespace NorthernLink.Notifications.Application.Dispatches.SendBookingPassesEmail;

/// <summary>
/// Handles <see cref="SendBookingPassesEmailCommand"/>: replay check by dispatch id → validate
/// the booking anchor → gate travellers + recipients (shared
/// <see cref="BookingPassesRecipientGate"/>) → compose once through the shared
/// <see cref="BookingPassesEmailComposer"/> → one provider batch call, no attachments →
/// record the outcomes as an <see cref="EmailDispatch"/>. Always returns the dispatch
/// response on success paths — including total provider failure, which is history the
/// dispatcher must see (HTTP 200). Mirrors
/// <see cref="SendClientAccrualsEmail.SendClientAccrualsEmailCommandHandler"/> minus the PDF.
/// </summary>
public sealed class SendBookingPassesEmailCommandHandler(
    IEmailDispatchRepository dispatchRepository,
    IEmailSender emailSender)
    : ICommandHandler<SendBookingPassesEmailCommand, EmailDispatchResponse>
{
    public async Task<Result<EmailDispatchResponse>> Handle(
        SendBookingPassesEmailCommand command,
        CancellationToken cancellationToken)
    {
        // Idempotent replay: the dispatch id is the aggregate id, so a re-POST (retry after
        // a timeout, double click) returns the stored outcomes without emailing anyone twice.
        var existing = await dispatchRepository.GetByIdAsync(command.DispatchId, cancellationToken);
        if (existing is not null)
        {
            return Result.Success(EmailDispatchResponseMapper.ToResponse(existing));
        }

        // Booking anchor gate — checked here, before anything is sent: the domain factory
        // re-validates at record time, but by then the batch would already be out the door.
        if (command.BookingId == Guid.Empty || string.IsNullOrWhiteSpace(command.BookingReference))
        {
            return Result.Failure<EmailDispatchResponse>(EmailDispatchErrors.BookingRequired);
        }

        var recipientsResult = BookingPassesRecipientGate.Normalize(command.Sheet, command.Recipients);
        if (recipientsResult.IsFailure)
        {
            return Result.Failure<EmailDispatchResponse>(recipientsResult.Error);
        }

        var recipients = recipientsResult.Value;
        var composition = BookingPassesEmailComposer.Compose(command.Sheet);

        // Every recipient gets the identical email — one provider batch call, no attachments.
        var outgoing = recipients
            .Select(recipient => new OutgoingEmail(
                recipient.Email, composition.Subject, composition.HtmlBody, composition.TextBody, null))
            .ToList();

        var outcomes = await emailSender.SendBatchAsync(outgoing, cancellationToken);

        var results = recipients
            .Select((recipient, index) =>
            {
                // The sender contract aligns outcomes by index; a short list (which would be
                // a sender bug) degrades to a failed outcome rather than an exception.
                var outcome = index < outcomes.Count
                    ? outcomes[index]
                    : new EmailSendOutcome(false, "Postmark.MissingResult", "The provider returned no result for this recipient.", null);

                return new DispatchRecipient
                {
                    Email = recipient.Email,
                    // The jsonb slot is the dispatch's display-name field; for a passes send
                    // that's the recipient's contact name (the customer, or the dispatcher's own copy).
                    PassengerName = recipient.ContactName,
                    Status = outcome.Sent ? DispatchRecipientStatus.Sent : DispatchRecipientStatus.Failed,
                    ErrorCode = outcome.ErrorCode,
                    ErrorMessage = outcome.ErrorMessage,
                    PostmarkMessageId = outcome.MessageId,
                };
            })
            .ToList();

        var dispatchResult = EmailDispatch.RecordBookingPasses(
            command.DispatchId,
            command.TenantId,
            command.BookingId,
            command.BookingReference,
            results);

        if (dispatchResult.IsFailure)
        {
            return Result.Failure<EmailDispatchResponse>(dispatchResult.Error);
        }

        dispatchRepository.Add(dispatchResult.Value);
        await dispatchRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(EmailDispatchResponseMapper.ToResponse(dispatchResult.Value));
    }
}
