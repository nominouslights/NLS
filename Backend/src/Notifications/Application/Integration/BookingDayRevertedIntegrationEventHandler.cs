using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Booking;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Rendering;
using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Domain.Dispatches;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Application.Integration;

/// <summary>
/// The module's first event-driven send (US-B.12): <c>booking.booking-day-reverted</c>
/// arrives by outbox polling and this handler emails every customer in the event's
/// recipient snapshot that their community trip is at risk — "{{SeatsNeeded}} seats
/// needed". The event carries the full snapshot because Notifications never queries other
/// modules; trip context is recorded as opaque snapshots like every dispatch.
/// <para>
/// Body: a built-in default, overridden by the tenant's newest active, client-unpinned
/// <see cref="NotificationServiceType.CommunityBookingAtRisk"/> template when one exists.
/// Recipients are chunked into <see cref="EmailDispatch.MaxRecipients"/>-sized dispatches
/// (the aggregate's hard cap); each chunk's dispatch id derives deterministically from
/// EventId + chunk index, so a redelivered event finds its chunks already recorded and
/// sends nothing twice — the same replay contract as the send endpoint, without a client
/// to mint the id. Chunks are recorded as they complete, so a crash mid-event resends only
/// the unrecorded tail.
/// </para>
/// </summary>
public sealed partial class BookingDayRevertedIntegrationEventHandler(
    IEmailTemplateRepository templates,
    IEmailDispatchRepository dispatches,
    IEmailSender emailSender,
    ILogger<BookingDayRevertedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<BookingDayRevertedIntegrationEvent>
{
    /// <summary>Template name recorded on dispatches sent with the built-in body.</summary>
    public const string BuiltInTemplateName = "Built-in: community trip at risk";

    /// <summary>Subject used when no CommunityBookingAtRisk template overrides it.</summary>
    public const string BuiltInSubject =
        "Trip at risk — {{SeatsNeeded}} seat(s) needed for {{Route}} on {{TripDate}}";

    /// <summary>HTML body used when no CommunityBookingAtRisk template overrides it.</summary>
    public const string BuiltInHtmlBody =
        """
        <p>Hello {{PassengerName}},</p>
        <p>Your Northern Link community shuttle on the <strong>{{Route}}</strong> corridor for
        <strong>{{TripDate}}</strong> has dropped below its passenger minimum and is at risk of not running.</p>
        <p><strong>{{SeatsNeeded}} more seat(s) are needed to save this trip.</strong>
        Cover the remaining seats, or invite fellow travellers to book, and the trip is back on.</p>
        <p>If the minimum is not reached before departure, the trip will not run and we will contact you
        about your booking.</p>
        <p>— Northern Link Shuttle &amp; Cargo</p>
        """;

    public async Task Handle(
        BookingDayRevertedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            var recipients = integrationEvent.Recipients
                .Where(r => EmailRegex().IsMatch(r.Email?.Trim() ?? string.Empty))
                .ToList();

            if (recipients.Count == 0)
            {
                logger.LogInformation(
                    "Booking day {BookingDayId} reverted with no reachable recipients; nothing to send ({EventId})",
                    integrationEvent.BookingDayId, integrationEvent.EventId);
                return;
            }

            var template = await templates.GetActiveByServiceTypeAsync(
                integrationEvent.TenantId, NotificationServiceType.CommunityBookingAtRisk, cancellationToken);
            var subjectTemplate = template?.Subject ?? BuiltInSubject;
            var htmlTemplate = template?.HtmlBody ?? BuiltInHtmlBody;

            var chunks = recipients
                .Chunk(EmailDispatch.MaxRecipients)
                .ToList();

            for (var chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
            {
                var dispatchId = DeterministicDispatchId(integrationEvent.EventId, chunkIndex);
                var existing = await dispatches.GetByIdAsync(dispatchId, cancellationToken);
                if (existing is not null)
                {
                    // Redelivery — this chunk already went out and its outcomes are history.
                    continue;
                }

                await SendChunkAsync(
                    integrationEvent, template, subjectTemplate, htmlTemplate,
                    chunks[chunkIndex], dispatchId, cancellationToken);
            }
        }
    }

    private async Task SendChunkAsync(
        BookingDayRevertedIntegrationEvent integrationEvent,
        EmailTemplate? template,
        string subjectTemplate,
        string htmlTemplate,
        IReadOnlyList<BookingDayRevertRecipient> chunk,
        Guid dispatchId,
        CancellationToken cancellationToken)
    {
        var outgoing = chunk
            .Select(recipient =>
            {
                var values = BuildValues(integrationEvent, recipient);
                var subject = MergeFieldRenderer.RenderSubject(subjectTemplate, values);
                var html = MergeFieldRenderer.RenderHtml(htmlTemplate, values);
                return new OutgoingEmail(recipient.Email.Trim(), subject, html, MergeFieldRenderer.RenderText(html));
            })
            .ToList();

        var outcomes = await emailSender.SendBatchAsync(outgoing, cancellationToken);

        var results = chunk
            .Select((recipient, index) =>
            {
                var outcome = index < outcomes.Count
                    ? outcomes[index]
                    : new EmailSendOutcome(false, "Postmark.MissingResult", "The provider returned no result for this recipient.", null);

                return new DispatchRecipient
                {
                    Email = recipient.Email.Trim(),
                    PassengerName = recipient.Name.Trim(),
                    Status = outcome.Sent ? DispatchRecipientStatus.Sent : DispatchRecipientStatus.Failed,
                    ErrorCode = outcome.ErrorCode,
                    ErrorMessage = outcome.ErrorMessage,
                    PostmarkMessageId = outcome.MessageId,
                };
            })
            .ToList();

        // Trip context is an opaque snapshot; the day's trip may not have been linked yet
        // (it is created asynchronously), so the booking day stands in for it — the record
        // must exist either way, because it doubles as the replay marker.
        var dispatchResult = EmailDispatch.Record(
            dispatchId,
            integrationEvent.TenantId,
            tripId: integrationEvent.TripId ?? integrationEvent.BookingDayId,
            tripNumber: integrationEvent.TripNumber
                ?? $"DAY-{integrationEvent.ServiceDate:yyyy-MM-dd}",
            manifestId: null,
            templateId: template?.Id ?? Guid.Empty,
            templateName: template?.Name ?? BuiltInTemplateName,
            serviceType: NotificationServiceType.CommunityBookingAtRisk,
            clientId: null,
            clientName: null,
            recipients: results);

        if (dispatchResult.IsFailure)
        {
            // Unreachable by construction (chunks are 1..MaxRecipients) — but a recording
            // failure must not dead-letter the event after the emails already went out.
            logger.LogError(
                "Reverted-day dispatch {DispatchId} could not be recorded: {Error} ({EventId})",
                dispatchId, dispatchResult.Error.Code, integrationEvent.EventId);
            return;
        }

        dispatches.Add(dispatchResult.Value);
        await dispatches.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Sent trip-at-risk email for booking day {BookingDayId} ({Corridor} {ServiceDate}) to {Count} recipient(s), dispatch {DispatchId} ({EventId})",
            integrationEvent.BookingDayId, integrationEvent.CorridorName, integrationEvent.ServiceDate,
            chunk.Count, dispatchId, integrationEvent.EventId);
    }

    private static Dictionary<string, string> BuildValues(
        BookingDayRevertedIntegrationEvent integrationEvent,
        BookingDayRevertRecipient recipient) =>
        new(StringComparer.Ordinal)
        {
            [MergeFields.PassengerName] = recipient.Name,
            [MergeFields.TripDate] = integrationEvent.ServiceDate.ToString(
                "dddd, MMMM d, yyyy", CultureInfo.InvariantCulture),
            [MergeFields.Route] = integrationEvent.CorridorName,
            [MergeFields.SeatsNeeded] = integrationEvent.SeatsNeeded.ToString(CultureInfo.InvariantCulture),
            [MergeFields.TripNumber] = integrationEvent.TripNumber ?? string.Empty,
        };

    /// <summary>
    /// SHA-256(eventId, chunk) folded into a Guid — the replay marker that makes at-least-once
    /// delivery safe without a client-minted dispatch id. Public because it IS the contract
    /// (a redelivered event must land on the same ids), and so tests can pin it.
    /// </summary>
    public static Guid DeterministicDispatchId(Guid eventId, int chunkIndex)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"booking-day-reverted:{eventId:N}:chunk:{chunkIndex}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    // RFC-lite, mirroring the send endpoint's gate: something@something.tld, no whitespace.
    // Invalid snapshot emails are skipped, never a failure — the event already excluded
    // customers with no email; this catches malformed ones.
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();
}
