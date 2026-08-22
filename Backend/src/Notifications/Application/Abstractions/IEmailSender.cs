namespace NorthernLink.Notifications.Application.Abstractions;

/// <summary>
/// Sends a batch of rendered emails through the provider. CONTRACT: implementations never
/// throw — a transport failure (auth, network, non-2xx) comes back as all-failed outcomes,
/// because per-recipient results are data the dispatcher must see, not an exception path.
/// The returned list aligns with <c>emails</c> by index and always has the same count.
/// </summary>
public interface IEmailSender
{
    Task<IReadOnlyList<EmailSendOutcome>> SendBatchAsync(
        IReadOnlyList<OutgoingEmail> emails,
        CancellationToken cancellationToken = default);
}

/// <summary>One fully rendered email ready for the provider.</summary>
public sealed record OutgoingEmail(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyList<EmailAttachment>? Attachments = null);

/// <summary>
/// One file attached to an outgoing email. <paramref name="Base64Content"/> is the raw file
/// bytes already Base64-encoded (Postmark's wire format); <paramref name="ContentType"/> is
/// the MIME type (e.g. <c>application/pdf</c>).
/// </summary>
public sealed record EmailAttachment(string Name, string Base64Content, string ContentType);

/// <summary>
/// The provider's verdict for one email. <paramref name="ErrorCode"/> is a stable machine
/// code (<c>Postmark.{code}</c>, <c>Postmark.Unauthorized</c>, <c>Postmark.Unreachable</c>,
/// <c>Postmark.Http{status}</c>); null when sent.
/// </summary>
public sealed record EmailSendOutcome(bool Sent, string? ErrorCode, string? ErrorMessage, string? MessageId);
