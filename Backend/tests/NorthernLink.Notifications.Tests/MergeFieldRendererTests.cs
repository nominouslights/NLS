using NorthernLink.Notifications.Application.Rendering;
using NorthernLink.Notifications.Domain.Templates;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>Merge-field substitution, HTML encoding of values, and the text fallback.</summary>
public class MergeFieldRendererTests
{
    /// <summary>
    /// Values for every canonical token, derived from <see cref="MergeFields.All"/> rather than
    /// hand-listed. A hand-listed dictionary is what let <c>SeatsNeeded</c> ship missing from
    /// <see cref="MergeFieldRenderer.SampleValues"/>: the old "every canonical token" test looped
    /// its own 11-entry list, so the 12th token was never rendered by anything.
    /// </summary>
    private static readonly Dictionary<string, string> Values =
        MergeFields.All.ToDictionary(
            token => token, token => $"value-for-{token}", StringComparer.Ordinal);

    [Fact]
    public void Every_canonical_token_substitutes_in_html()
    {
        var template = string.Join(" ", MergeFields.All.Select(token => $"{{{{{token}}}}}"));

        var rendered = MergeFieldRenderer.RenderHtml(template, Values);

        foreach (var token in MergeFields.All)
        {
            Assert.Contains(System.Net.WebUtility.HtmlEncode(Values[token]), rendered);
        }

        Assert.DoesNotContain("{{", rendered);
    }

    [Fact]
    public void The_preview_sample_data_covers_every_canonical_token()
    {
        // The preview endpoint renders with SampleValues when the caller supplies none
        // (PreviewEmailTemplateQueryHandler). A token missing from that dictionary renders as an
        // empty string — Substitute never leaks the raw token — so the dispatcher previews a
        // blank hole and has no way to tell it from a token that is genuinely empty. Looping the
        // production list is the point: a new MergeFields constant with no sample fails here.
        var missing = MergeFields.All
            .Where(token => !MergeFieldRenderer.SampleValues.ContainsKey(token))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"MergeFieldRenderer.SampleValues has no sample for: {string.Join(", ", missing)}.");

        var unknown = MergeFieldRenderer.SampleValues.Keys
            .Where(token => !MergeFields.All.Contains(token, StringComparer.Ordinal))
            .ToList();

        Assert.True(
            unknown.Count == 0,
            $"MergeFieldRenderer.SampleValues carries tokens that are not canonical: {string.Join(", ", unknown)}.");

        Assert.All(MergeFieldRenderer.SampleValues.Values, value => Assert.False(string.IsNullOrWhiteSpace(value)));
    }

    [Fact]
    public void A_preview_with_no_caller_values_renders_no_blank_holes()
    {
        // The end-to-end shape of the same bug: {{SeatsNeeded}} on the CommunityBookingAtRisk
        // template used to preview as "Trip at risk —  seat(s) needed".
        var template = string.Join(" | ", MergeFields.All.Select(token => $"{{{{{token}}}}}"));

        var rendered = MergeFieldRenderer.RenderHtml(template, MergeFieldRenderer.SampleValues);

        Assert.DoesNotContain("{{", rendered);
        Assert.All(rendered.Split('|'), segment => Assert.False(string.IsNullOrWhiteSpace(segment)));
    }

    [Fact]
    public void Tokens_tolerate_whitespace_inside_the_braces()
    {
        var rendered = MergeFieldRenderer.RenderHtml("Hi {{ PassengerName }}!", Values);

        Assert.Equal($"Hi {Values[MergeFields.PassengerName]}!", rendered);
    }

    [Fact]
    public void Missing_value_renders_empty_never_the_raw_token()
    {
        var rendered = MergeFieldRenderer.RenderHtml(
            "Client: {{ClientName}}.", new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Equal("Client: .", rendered);
    }

    [Fact]
    public void Html_values_are_encoded_so_a_hostile_name_cannot_inject_markup()
    {
        var hostile = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MergeFields.PassengerName] = "<script>alert('x')</script>",
        };

        var rendered = MergeFieldRenderer.RenderHtml("<p>Hi {{PassengerName}}</p>", hostile);

        Assert.DoesNotContain("<script>", rendered);
        Assert.Contains("&lt;script&gt;", rendered);
    }

    [Fact]
    public void Subject_substitutes_raw_values_without_html_encoding()
    {
        var subject = MergeFieldRenderer.RenderSubject("Pickup {{Route}}", Values);

        Assert.Equal($"Pickup {Values[MergeFields.Route]}", subject);
    }

    [Fact]
    public void Text_fallback_strips_tags_and_decodes_entities()
    {
        var html = "<div><p>Hi <strong>Alex &amp; family</strong>,</p><p>See you at 8:30.</p></div>";

        var text = MergeFieldRenderer.RenderText(html);

        Assert.DoesNotContain("<", text);
        Assert.Contains("Hi Alex & family,", text);
        Assert.Contains("See you at 8:30.", text);
    }

    [Fact]
    public void Text_fallback_drops_style_blocks_entirely()
    {
        var html = "<style>p { color: red; }</style><p>Visible</p>";

        var text = MergeFieldRenderer.RenderText(html);

        Assert.Equal("Visible", text);
    }

    [Fact]
    public void Unknown_tokens_report_from_the_canonical_set()
    {
        var unknown = MergeFields.UnknownTokensIn("Hi {{PassengerName}}, {{Typo}} and {{AnotherTypo}}");

        Assert.Equal(["Typo", "AnotherTypo"], unknown);
    }
}
