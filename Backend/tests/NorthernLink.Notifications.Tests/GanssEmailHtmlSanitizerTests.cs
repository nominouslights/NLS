using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Infrastructure.Rendering;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// Allowlist sanitization of stored/sent template bodies: strips script/injection vectors,
/// keeps common email formatting, and — critically — leaves <c>{{MergeTokens}}</c> untouched
/// so the send-time renderer can still substitute HTML-encoded values into them.
/// </summary>
public class GanssEmailHtmlSanitizerTests
{
    private readonly IEmailHtmlSanitizer _sanitizer = new GanssEmailHtmlSanitizer();

    [Fact]
    public void Script_tags_are_removed()
    {
        var clean = _sanitizer.Sanitize("<p>Hello</p><script>alert(1)</script>");

        Assert.DoesNotContain("<script", clean, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert(1)", clean);
        Assert.Contains("<p>Hello</p>", clean);
    }

    [Fact]
    public void Img_is_kept_but_event_handler_attributes_are_dropped()
    {
        var clean = _sanitizer.Sanitize("<img src=\"https://example.com/x.png\" onerror=\"alert(1)\">");

        Assert.Contains("<img", clean, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", clean, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert(1)", clean);
    }

    [Fact]
    public void Javascript_url_href_is_neutralized()
    {
        var clean = _sanitizer.Sanitize("<a href=\"javascript:alert(1)\">x</a>");

        Assert.DoesNotContain("javascript:", clean, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">x</a>", clean); // the anchor text survives; only the href is stripped
    }

    [Fact]
    public void Iframe_object_embed_and_form_are_removed()
    {
        var clean = _sanitizer.Sanitize(
            "<p>ok</p><iframe src=\"https://evil\"></iframe><object></object><embed><form><input></form>");

        Assert.Contains("<p>ok</p>", clean);
        Assert.DoesNotContain("<iframe", clean, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<object", clean, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<embed", clean, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", clean, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Safe_formatting_inline_styles_and_layout_are_preserved()
    {
        var clean = _sanitizer.Sanitize(
            "<p style=\"color:red\">Hi <strong>there</strong></p>" +
            "<table><thead><tr><th>H</th></tr></thead><tbody><tr><td>C</td></tr></tbody></table>" +
            "<a href=\"https://example.com\">link</a><a href=\"mailto:ops@example.com\">mail</a>");

        Assert.Contains("<strong>there</strong>", clean);
        Assert.Contains("color", clean); // inline style survives (value may be normalized, e.g. rgba(...))
        Assert.Contains("<table", clean, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<td>C</td>", clean);
        Assert.Contains("https://example.com", clean);
        Assert.Contains("mailto:ops@example.com", clean); // mailto scheme explicitly allowed
    }

    [Fact]
    public void Merge_tokens_survive_sanitization_unchanged()
    {
        var body = "<p style=\"color:red\">Hi {{PassengerName}}, your trip is {{TripNumber}}.</p>";

        var clean = _sanitizer.Sanitize(body);

        Assert.Contains("{{PassengerName}}", clean);
        Assert.Contains("{{TripNumber}}", clean);
    }
}
