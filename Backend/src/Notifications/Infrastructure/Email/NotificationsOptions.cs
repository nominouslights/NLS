namespace NorthernLink.Notifications.Infrastructure.Email;

/// <summary>
/// Bound from the "Notifications" configuration section (non-secret values only — the
/// Postmark server token is an environment variable, <c>Notifications__PostmarkApiKey</c>,
/// read lazily on the send path, never through configuration).
/// </summary>
public sealed class NotificationsOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Notifications";

    /// <summary>
    /// The From address stamped on every outgoing email. MUST be a verified Postmark
    /// sender signature once a real server token is in use — Postmark rejects sends from
    /// unverified addresses with ErrorCode 400.
    /// </summary>
    public string FromAddress { get; init; } = "emelio.campbell@northernlinkshuttleandcargo.com";

    /// <summary>The Postmark message stream to send through.</summary>
    public string MessageStream { get; init; } = "outbound";

    /// <summary>The Postmark API base URL (overridable for tests).</summary>
    public string PostmarkBaseUrl { get; init; } = "https://api.postmarkapp.com";
}
