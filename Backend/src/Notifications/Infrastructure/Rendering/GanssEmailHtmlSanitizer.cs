using Ganss.Xss;
using NorthernLink.Notifications.Application.Abstractions;

namespace NorthernLink.Notifications.Infrastructure.Rendering;

/// <summary>
/// <see cref="IEmailHtmlSanitizer"/> backed by the vetted Ganss <see cref="HtmlSanitizer"/>,
/// configured for transactional HTML email. We lean on the library defaults — which already
/// strip <c>&lt;script&gt;</c>/<c>&lt;iframe&gt;</c>/<c>&lt;object&gt;</c>/<c>&lt;embed&gt;</c>/
/// <c>&lt;form&gt;</c>, every <c>on*</c> event-handler attribute, and <c>javascript:</c> (and
/// other non-<c>http</c>) URL schemes — and widen only where email needs it:
/// <list type="bullet">
///   <item>the default tag allowlist already covers p/div/span/a/img, table + thead/tbody/tr/td/th,
///     ul/ol/li, h1–h6, strong/em/b/i/u, br, hr, blockquote, pre, code;</item>
///   <item>the default attribute allowlist already includes the inline <c>style</c> attribute
///     email templates rely on, plus <c>href</c>/<c>src</c>;</item>
///   <item>we add <c>mailto</c> to the URL scheme allowlist (defaults are http/https only) so
///     contact links survive; <c>data:</c> and <c>cid:</c> URLs are left disallowed.</item>
/// </list>
/// One instance is constructed and configured once at DI time and reused (registered singleton);
/// each <see cref="Sanitize"/> call is a self-contained parse with no shared mutable state.
/// Merge tokens (<c>{{PassengerName}}</c> …) live in text/attribute-text nodes, which a parser
/// preserves verbatim, so they pass through untouched for the send-time renderer to substitute.
/// </summary>
public sealed class GanssEmailHtmlSanitizer : IEmailHtmlSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public GanssEmailHtmlSanitizer()
    {
        _sanitizer = new HtmlSanitizer();

        // Defaults are http/https only; transactional emails commonly carry mailto: links.
        _sanitizer.AllowedSchemes.Add("mailto");

        // The Ganss default tag allowlist DOES include <form> and its controls; a transactional
        // email must never carry an interactive form, so drop the whole family explicitly.
        foreach (var tag in new[] { "form", "input", "button", "textarea", "select", "option", "label", "fieldset" })
        {
            _sanitizer.AllowedTags.Remove(tag);
        }
    }

    public string Sanitize(string html) =>
        string.IsNullOrEmpty(html) ? html : _sanitizer.Sanitize(html);
}
