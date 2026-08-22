namespace NorthernLink.Notifications.Application.Abstractions;

/// <summary>
/// Allowlist-sanitizes a stored/sent email-template HTML body — defense-in-depth against
/// stored script injection in the raw template markup itself (merge VALUES are already
/// HTML-encoded at render time). Sanitization happens at SAVE, on the token-bearing template,
/// so it must leave <c>{{MergeTokens}}</c> in text nodes intact. Keeps common transactional
/// email formatting/layout tags and inline styles; drops <c>&lt;script&gt;</c>,
/// <c>&lt;iframe&gt;</c>, event-handler attributes, and <c>javascript:</c>/other non-http URLs.
/// The concrete implementation lives in Infrastructure so this layer stays off the third-party dep.
/// </summary>
public interface IEmailHtmlSanitizer
{
    /// <summary>Returns the sanitized HTML. Never throws for well-formed input; never null.</summary>
    string Sanitize(string html);
}
