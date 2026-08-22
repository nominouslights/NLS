using System.Text.RegularExpressions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Rendering;
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
    IEmailSender emailSender)
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

        var outgoing = command.Recipients
            .Select(recipient =>
            {
                var values = BuildValues(command, recipient);
                var subject = MergeFieldRenderer.RenderSubject(template.Subject, values);
                var htmlBody = MergeFieldRenderer.RenderHtml(template.HtmlBody, values);
                return new OutgoingEmail(recipient.Email.Trim(), subject, htmlBody, MergeFieldRenderer.RenderText(htmlBody));
            })
            .ToList();

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

        return Result.Success(EmailDispatchResponseMapper.ToResponse(dispatchResult.Value));
    }

    private static Dictionary<string, string> BuildValues(
        SendTripPickupEmailCommand command,
        RecipientInput recipient) =>
        new(StringComparer.Ordinal)
        {
            [MergeFields.PassengerName] = recipient.PassengerName,
            [MergeFields.TripDate] = command.TripDate,
            [MergeFields.PickupTime] = command.PickupTime,
            [MergeFields.DropoffTime] = command.DropoffTime,
            [MergeFields.Route] = command.Route,
            [MergeFields.PickupStop] = recipient.PickupStop ?? string.Empty,
            [MergeFields.PickupAddress] = recipient.PickupAddress ?? string.Empty,
            [MergeFields.DropoffStop] = recipient.DropoffStop ?? string.Empty,
            [MergeFields.DropoffStopAddress] = recipient.DropoffStopAddress ?? string.Empty,
            [MergeFields.TripNumber] = command.TripNumber,
            [MergeFields.ClientName] = command.ClientName ?? string.Empty,
        };

    // RFC-lite, mirroring the frontend's isEmailContact gate: something@something.tld,
    // no whitespace. Deliberately loose — Postmark is the real arbiter.
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();
}
