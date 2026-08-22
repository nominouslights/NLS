using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Domain.Dispatches;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Application.Dispatches.SendTripPickupEmail;

/// <summary>
/// Handles <see cref="SendTripPickupEmailCommand"/>: replay check by dispatch id → load and
/// gate the template (must exist, be active, and match the trip's service type and client
/// pin) → validate recipients (1–16, RFC-lite email)
/// → render subject/HTML/text per recipient → one provider batch call → record the outcomes
/// as an <see cref="EmailDispatch"/>. Always returns the dispatch response on success paths —
/// including total provider failure, which is history the dispatcher must see (HTTP 200).
/// </summary>
public sealed partial class SendTripPickupEmailCommandHandler(
    IEmailTemplateRepository templateRepository,
    IEmailDispatchRepository dispatchRepository,
    IEmailSender emailSender,
    IPickupEmailReportPdf reportPdf,
    ILogger<SendTripPickupEmailCommandHandler> logger)
    : ICommandHandler<SendTripPickupEmailCommand, EmailDispatchResponse>
{
    public async Task<Result<EmailDispatchResponse>> Handle(
        SendTripPickupEmailCommand command,
        CancellationToken cancellationToken)
    {
        // Idempotent replay: the dispatch id is the aggregate id, so a re-POST (retry after
        // a timeout, double click) returns the stored outcomes without emailing anyone twice.
        var existing = await dispatchRepository.GetByIdAsync(command.DispatchId, cancellationToken);
        if (existing is not null)
        {
            return Result.Success(EmailDispatchResponseMapper.ToResponse(existing));
        }

        var template = await templateRepository.GetByIdAsync(command.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure<EmailDispatchResponse>(EmailTemplateErrors.NotFound);
        }

        if (!template.IsActive)
        {
            return Result.Failure<EmailDispatchResponse>(EmailTemplateErrors.Inactive);
        }

        if (template.ServiceType != command.ServiceType)
        {
            return Result.Failure<EmailDispatchResponse>(EmailTemplateErrors.ServiceTypeMismatch);
        }

        // A service-wide template (null ClientId) is valid for any trip of its service type;
        // a client-pinned one only for that exact client (a client-less trip never matches).
        if (template.ClientId is not null && template.ClientId != command.ClientId)
        {
            return Result.Failure<EmailDispatchResponse>(EmailTemplateErrors.ClientMismatch);
        }

        if (command.Recipients.Count == 0)
        {
            return Result.Failure<EmailDispatchResponse>(EmailDispatchErrors.NoRecipients);
        }

        if (command.Recipients.Count > EmailDispatch.MaxRecipients)
        {
            return Result.Failure<EmailDispatchResponse>(EmailDispatchErrors.TooManyRecipients);
        }

        foreach (var recipient in command.Recipients)
        {
            if (!EmailRegex().IsMatch(recipient.Email?.Trim() ?? string.Empty))
            {
                return Result.Failure<EmailDispatchResponse>(
                    EmailDispatchErrors.InvalidRecipientEmail(recipient.Email ?? string.Empty));
            }
        }

        var outgoing = PickupEmailRenderer.Render(
            template,
            command.TripDate,
            command.PickupTime,
            command.DropoffTime,
            command.Route,
            command.TripNumber,
            command.ClientName,
            command.Recipients);

        var outcomes = await emailSender.SendBatchAsync(outgoing, cancellationToken);

        var results = command.Recipients
            .Select((recipient, index) =>
            {
                // The sender contract aligns outcomes by index; a short list (which would be
                // a sender bug) degrades to a failed outcome rather than an exception.
                var outcome = index < outcomes.Count
                    ? outcomes[index]
                    : new EmailSendOutcome(false, "Postmark.MissingResult", "The provider returned no result for this recipient.", null);

                return new DispatchRecipient
                {
                    Email = recipient.Email.Trim(),
                    PassengerName = recipient.PassengerName.Trim(),
                    Status = outcome.Sent ? DispatchRecipientStatus.Sent : DispatchRecipientStatus.Failed,
                    ErrorCode = outcome.ErrorCode,
                    ErrorMessage = outcome.ErrorMessage,
                    PostmarkMessageId = outcome.MessageId,
                };
            })
            .ToList();

        var dispatchResult = EmailDispatch.Record(
            command.DispatchId,
            command.TenantId,
            command.TripId,
            command.TripNumber,
            command.ManifestId,
            template.Id,
            template.Name,
            command.ServiceType,
            command.ClientId,
            command.ClientName,
            results);

        if (dispatchResult.IsFailure)
        {
            return Result.Failure<EmailDispatchResponse>(dispatchResult.Error);
        }

        dispatchRepository.Add(dispatchResult.Value);
        await dispatchRepository.SaveChangesAsync(cancellationToken);

        // The pickup result is now fixed. Anything below is best-effort and must never change it.
        var response = EmailDispatchResponseMapper.ToResponse(dispatchResult.Value);

        await TrySendPickupReportAsync(command, template.Name, outgoing, outcomes, cancellationToken);

        return Result.Success(response);
    }

    /// <summary>
    /// Best-effort report step: for a ContractCrew trip with at least one valid report recipient,
    /// build a PDF of every sent pickup email and email it to those recipients. Wrapped entirely in
    /// try/catch — a report failure is logged and swallowed so the pickup send's result is returned
    /// unchanged. Never recorded as an <see cref="EmailDispatch"/>.
    /// </summary>
    private async Task TrySendPickupReportAsync(
        SendTripPickupEmailCommand command,
        string templateName,
        IReadOnlyList<OutgoingEmail> outgoing,
        IReadOnlyList<EmailSendOutcome> outcomes,
        CancellationToken cancellationToken)
    {
        try
        {
            if (command.ServiceType != NotificationServiceType.ContractCrew)
            {
                return;
            }

            var reportRecipients = command.ReportRecipients
                .Select(address => address?.Trim() ?? string.Empty)
                .Where(address => EmailRegex().IsMatch(address))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (reportRecipients.Count == 0)
            {
                return;
            }

            var items = command.Recipients
                .Select((recipient, index) =>
                {
                    var sent = index < outcomes.Count && outcomes[index].Sent;
                    var email = outgoing[index];
                    return new PickupEmailReportItem(
                        recipient.PassengerName.Trim(),
                        recipient.Email.Trim(),
                        sent ? "Sent" : "Failed",
                        email.Subject,
                        email.TextBody);
                })
                .ToList();

            var sentCount = outcomes.Count(outcome => outcome.Sent);
            var failedCount = outcomes.Count - sentCount;

            var composition = PickupEmailReportComposer.Compose(
                new PickupReportContext(
                    command.TripNumber,
                    command.Route,
                    command.TripDate,
                    templateName,
                    DateTimeOffset.UtcNow,
                    sentCount,
                    failedCount,
                    items),
                reportPdf);

            var attachment = new EmailAttachment(
                $"pickup-emails-{command.TripNumber}.pdf",
                Convert.ToBase64String(composition.PdfBytes),
                "application/pdf");

            var reportEmails = reportRecipients
                .Select(to => new OutgoingEmail(to, composition.Subject, composition.HtmlBody, composition.TextBody, [attachment]))
                .ToList();

            await emailSender.SendBatchAsync(reportEmails, cancellationToken);
        }
        catch (Exception exception)
        {
            // Report delivery is best-effort — the pickup send already succeeded and its result
            // stands. Log and move on rather than surface a failure to the caller.
            logger.LogWarning(
                exception,
                "Pickup-email report step failed for trip {TripNumber} (dispatch {DispatchId}); the pickup send is unaffected.",
                command.TripNumber,
                command.DispatchId);
        }
    }

    // RFC-lite, mirroring the frontend's isEmailContact gate: something@something.tld,
    // no whitespace. Deliberately loose — Postmark is the real arbiter.
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();
}
