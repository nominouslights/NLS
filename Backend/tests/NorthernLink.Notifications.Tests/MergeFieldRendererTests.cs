using NorthernLink.Notifications.Application.Rendering;
using NorthernLink.Notifications.Domain.Templates;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>Merge-field substitution, HTML encoding of values, and the text fallback.</summary>
public class MergeFieldRendererTests
{
    private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
    {
        [MergeFields.PassengerName] = "Alex Moody",
        [MergeFields.TripDate] = "Tuesday, August 4, 2026",
        [MergeFields.PickupTime] = "8:30 AM",
        [MergeFields.Route] = "Thompson – Lynn Lake",
        [MergeFields.PickupStop] = "Thompson Terminal",
        [MergeFields.PickupAddress] = "12 Station Rd, Thompson, MB R8N 0A1",
        [MergeFields.DropoffStop] = "Lynn Lake Co-op",
        [MergeFields.DropoffStopAddress] = "5 Co-op Lane, Lynn Lake, MB R0B 0W0",
        [MergeFields.TripNumber] = "NL-1042",
        [MergeFields.ClientName] = "Marcel Colomb First Nation",
    };

    [Fact]
    public void Every_canonical_token_substitutes_in_html()
    {
        var template = string.Join(" ", MergeFields.All.Select(token => $"{{{{{token}}}}}"));

        var rendered = MergeFieldRenderer.RenderHtml(template, Values);

        foreach (var value in Values.Values)
        {
            Assert.Contains(System.Net.WebUtility.HtmlEncode(value), rendered);
        }

        Assert.DoesNotContain("{{", rendered);
    }

    [Fact]
    public void Tokens_tolerate_whitespace_inside_the_braces()
    {
        var rendered = MergeFieldRenderer.RenderHtml("Hi {{ PassengerName }}!", Values);

        Assert.Equal("Hi Alex Moody!", rendered);
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

        Assert.Equal("Pickup Thompson – Lynn Lake", subject);
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
