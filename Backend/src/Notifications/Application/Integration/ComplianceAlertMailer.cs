using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Rendering;
using NorthernLink.Notifications.Domain.Dispatches;

namespace NorthernLink.Notifications.Application.Integration;

/// <summary>
/// One compliance alert to send: who it is for (by role), and the rendered subject and body.
/// <paramref name="Kind"/> is the alert's stable name ("eld-malfunction-open", …) — it is part
/// of the deterministic dispatch id, so an "open" and a later "resolved" alert for the same
/// report never collide even if they ever shared an event id. <paramref name="HtmlBody"/> must
/// already be HTML-safe (free text encoded) — the mailer renders the text alternative from it.
/// </summary>
public sealed record ComplianceAlert(
    Guid TenantId,
    Guid EventId,
    string Kind,
    IReadOnlyCollection<string> AudienceRoles,
    string TemplateName,
    string Subject,
    string HtmlBody);

/// <summary>What became of a <see cref="ComplianceAlert"/>: how many emails went out, and how many chunks a redelivery skipped.</summary>
public sealed record ComplianceAlertOutcome(int RecipientsEmailed, int ChunksSkipped)
{
    public static readonly ComplianceAlertOutcome NoRecipients = new(0, 0);
}

/// <summary>
/// The send half shared by every hours-of-service compliance alert: selects the audience from
/// the <c>user_lookup</c> replica (active users of the tenant whose role is in the alert's
/// list), sends the built-in email through <see cref="IEmailSender"/>, and records an
/// <see cref="EmailDispatch"/> under a <b>deterministic id derived from the integration event's
/// id</b> — so a redelivered event finds its dispatch already on record and sends nothing twice,
/// the same replay contract as <c>BookingDayRevertedIntegrationEventHandler</c>. Recipients are
/// chunked into <see cref="EmailDispatch.MaxRecipients"/>-sized dispatches (the aggregate's hard
/// cap) and each chunk is recorded as it completes, so a crash mid-event resends only the tail.
/// <para>
/// No audience is not a failure: the event is logged and nothing is recorded, so the consumer
/// marks it delivered rather than parking it — a tenant with no compliance officer yet is a
/// configuration fact, not a dead letter.
/// </para>
/// </summary>
public sealed partial class ComplianceAlertMailer(
    IUserLookupRepository users,
    IEmailDispatchRepository dispatches,
    IEmailSender emailSender,
    ILogger<ComplianceAlertMailer> logger)
{
    /// <summary>The ledger's zone — every instant in an alert is shown as Winnipeg wall-clock time.</summary>
    public const string TimeZoneId = "America/Winnipeg";

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public async Task<ComplianceAlertOutcome> SendAsync(ComplianceAlert alert, CancellationToken cancellationToken)
    {
        var audience = await users.ListActiveByRolesAsync(alert.TenantId, alert.AudienceRoles, cancellationToken);

        var recipients = audience
            .Where(u => EmailRegex().IsMatch(u.Email?.Trim() ?? string.Empty))
            .DistinctBy(u => u.Email.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
        {
            logger.LogWarning(
                "Compliance alert {Kind} for tenant {TenantId} has no active recipient in roles [{Roles}]; nothing sent ({EventId})",
                alert.Kind, alert.TenantId, string.Join(", ", alert.AudienceRoles), alert.EventId);
            return ComplianceAlertOutcome.NoRecipients;
        }

        var chunks = recipients.Chunk(EmailDispatch.MaxRecipients).ToList();
        var emailed = 0;
        var skipped = 0;

        for (var chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
        {
            var dispatchId = DeterministicDispatchId(alert.Kind, alert.EventId, chunkIndex);
            var existing = await dispatches.GetByIdAsync(dispatchId, cancellationToken);
            if (existing is not null)
            {
                // Redelivery — this chunk already went out and its outcomes are history.
                skipped++;
                continue;
            }

            emailed += await SendChunkAsync(alert, chunks[chunkIndex], dispatchId, cancellationToken);
        }

        return new ComplianceAlertOutcome(emailed, skipped);
    }

    private async Task<int> SendChunkAsync(
        ComplianceAlert alert,
        IReadOnlyList<UserLookup> chunk,
        Guid dispatchId,
        CancellationToken cancellationToken)
    {
        var textBody = MergeFieldRenderer.RenderText(alert.HtmlBody);
        var outgoing = chunk
            .Select(user => new OutgoingEmail(user.Email.Trim(), alert.Subject, alert.HtmlBody, textBody))
            .ToList();

        var outcomes = await emailSender.SendBatchAsync(outgoing, cancellationToken);

        var results = chunk
            .Select((user, index) =>
            {
                var outcome = index < outcomes.Count
                    ? outcomes[index]
                    : new EmailSendOutcome(false, "Postmark.MissingResult", "The provider returned no result for this recipient.", null);

                return new DispatchRecipient
                {
                    Email = user.Email.Trim(),
                    PassengerName = user.DisplayName.Trim(),
                    Status = outcome.Sent ? DispatchRecipientStatus.Sent : DispatchRecipientStatus.Failed,
                    ErrorCode = outcome.ErrorCode,
                    ErrorMessage = outcome.ErrorMessage,
                    PostmarkMessageId = outcome.MessageId,
                };
            })
            .ToList();

        var dispatchResult = EmailDispatch.RecordComplianceAlert(dispatchId, alert.TenantId, alert.TemplateName, results);
        if (dispatchResult.IsFailure)
        {
            // Unreachable by construction (chunks are 1..MaxRecipients, the name is a constant)
            // — but a recording failure must not dead-letter the event after the emails went out.
            logger.LogError(
                "Compliance alert dispatch {DispatchId} ({Kind}) could not be recorded: {Error} ({EventId})",
                dispatchId, alert.Kind, dispatchResult.Error.Code, alert.EventId);
            return chunk.Count;
        }

        dispatches.Add(dispatchResult.Value);
        await dispatches.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Sent compliance alert {Kind} to {Count} recipient(s), dispatch {DispatchId} ({EventId})",
            alert.Kind, chunk.Count, dispatchId, alert.EventId);
        return chunk.Count;
    }

    /// <summary>
    /// SHA-256("compliance-alert:{kind}:{eventId:N}:chunk:{i}") folded into a Guid — the replay
    /// marker that makes at-least-once delivery safe without a client-minted dispatch id. Public
    /// because it IS the contract (a redelivered event must land on the same ids), and so tests
    /// can pin the values.
    /// </summary>
    public static Guid DeterministicDispatchId(string kind, Guid eventId, int chunkIndex)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"compliance-alert:{kind}:{eventId:N}:chunk:{chunkIndex}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    /// <summary>
    /// An instant as the alert shows it: Winnipeg wall-clock, spelled out, with the zone named
    /// so a reader on a phone in another province is not misled — e.g.
    /// "Monday, September 14, 2026 at 15:00 (Winnipeg time)".
    /// </summary>
    public static string LocalTime(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, Zone)
            .ToString("dddd, MMMM d, yyyy 'at' HH:mm", CultureInfo.InvariantCulture)
        + " (Winnipeg time)";

    /// <summary>Free text from an event, made safe to drop into the HTML body.</summary>
    public static string Html(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    // RFC-lite, mirroring the send endpoint's gate: something@something.tld, no whitespace.
    // A malformed replica email is skipped, never a failure.
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();
}
