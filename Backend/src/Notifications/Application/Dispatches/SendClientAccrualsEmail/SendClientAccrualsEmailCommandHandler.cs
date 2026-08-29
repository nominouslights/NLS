using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Domain.Dispatches;

namespace NorthernLink.Notifications.Application.Dispatches.SendClientAccrualsEmail;

/// <summary>
/// Handles <see cref="SendClientAccrualsEmailCommand"/>: replay check by dispatch id →
/// validate the client snapshot and recipients (1–16 distinct, RFC-lite email, via the shared
/// <see cref="AccrualsRecipientGate"/>) → compose the covering email + report PDF through the
/// shared <see cref="ClientAccrualsEmailComposer"/> → one provider batch call with the PDF
/// attached to every recipient → record the outcomes as an <see cref="EmailDispatch"/>.
/// Always returns the dispatch response on success paths — including total provider failure,
/// which is history the dispatcher must see (HTTP 200). Mirrors
/// <see cref="SendTripPickupEmail.SendTripPickupEmailCommandHandler"/>, minus the template
/// machinery: the report body arrives fully composed, so there is no template to load or gate.
/// </summary>
public sealed class SendClientAccrualsEmailCommandHandler(
    IEmailDispatchRepository dispatchRepository,
    IEmailSender emailSender,
    IClientAccrualsReportPdf reportPdf)
    : ICommandHandler<SendClientAccrualsEmailCommand, EmailDispatchResponse>
{
    public async Task<Result<EmailDispatchResponse>> Handle(
        SendClientAccrualsEmailCommand command,
        CancellationToken cancellationToken)
    {
        // Idempotent replay: the dispatch id is the aggregate id, so a re-POST (retry after
        // a timeout, double click) returns the stored outcomes without emailing anyone twice.
        var existing = await dispatchRepository.GetByIdAsync(command.DispatchId, cancellationToken);
        if (existing is not null)
        {
            return Result.Success(EmailDispatchResponseMapper.ToResponse(existing));
        }

        // Client snapshot gate — checked here, before anything is sent: the domain factory
        // re-validates at record time, but by then the batch would already be out the door.
        if (command.ClientId == Guid.Empty || string.IsNullOrWhiteSpace(command.ClientName))
        {
            return Result.Failure<EmailDispatchResponse>(EmailDispatchErrors.ClientRequired);
        }

        var recipientsResult = AccrualsRecipientGate.Normalize(command.Recipients);
        if (recipientsResult.IsFailure)
        {
            return Result.Failure<EmailDispatchResponse>(recipientsResult.Error);
        }

        var recipients = recipientsResult.Value;

        var composition = ClientAccrualsEmailComposer.Compose(command.Report, reportPdf);
        var attachment = new EmailAttachment(
            ClientAccrualsEmailComposer.AttachmentName(command.Report),
            Convert.ToBase64String(composition.PdfBytes),
            "application/pdf");

        // Every recipient gets the identical covering email + PDF — one provider batch call.
        var outgoing = recipients
            .Select(recipient => new OutgoingEmail(
                recipient.Email, composition.Subject, composition.HtmlBody, composition.TextBody, [attachment]))
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
                    // The jsonb slot is the dispatch's display-name field; for an accruals
                    // send that's the contact's name rather than a passenger's.
                    PassengerName = recipient.ContactName,
                    Status = outcome.Sent ? DispatchRecipientStatus.Sent : DispatchRecipientStatus.Failed,
                    ErrorCode = outcome.ErrorCode,
                    ErrorMessage = outcome.ErrorMessage,
                    PostmarkMessageId = outcome.MessageId,
                };
            })
            .ToList();

        var dispatchResult = EmailDispatch.RecordClientAccruals(
            command.DispatchId,
            command.TenantId,
            command.ClientId,
            command.ClientName,
            command.ServiceType,
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
