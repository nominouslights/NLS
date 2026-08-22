using System.Net;
using System.Text.RegularExpressions;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Application.Rendering;

/// <summary>
/// The one merge-field renderer — the send path and the preview endpoint both go through
/// here, so what the dispatcher previews is exactly what the passenger receives. Merge
/// values are untrusted manifest data: <see cref="RenderHtml"/> HTML-encodes every value
/// before substitution so a hostile passenger name can never inject markup.
/// </summary>
public static partial class MergeFieldRenderer
{
    /// <summary>Server sample data used when a preview request supplies no values.</summary>
    public static IReadOnlyDictionary<string, string> SampleValues { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MergeFields.PassengerName] = "Alex Moody",
            [MergeFields.TripDate] = "Tuesday, August 4, 2026",
            [MergeFields.PickupTime] = "8:30 AM",
            [MergeFields.DropoffTime] = "11:15 AM",
            [MergeFields.Route] = "Thompson – Lynn Lake",
            [MergeFields.PickupStop] = "Thompson Terminal",
            [MergeFields.PickupAddress] = "12 Station Rd, Thompson, MB R8N 0A1",
            [MergeFields.DropoffStop] = "Lynn Lake Co-op",
            [MergeFields.DropoffStopAddress] = "5 Co-op Lane, Lynn Lake, MB R0B 0W0",
            [MergeFields.TripNumber] = "NL-1042",
            [MergeFields.ClientName] = "Marcel Colomb First Nation",
        };

    /// <summary>Substitutes values into a subject line (plain text — no encoding).</summary>
    public static string RenderSubject(string subject, IReadOnlyDictionary<string, string> values) =>
        MergeFields.Substitute(subject, values, static value => value);

    /// <summary>Substitutes HTML-encoded values into the HTML body.</summary>
    public static string RenderHtml(string htmlBody, IReadOnlyDictionary<string, string> values) =>
        MergeFields.Substitute(htmlBody, values, WebUtility.HtmlEncode);

    /// <summary>
    /// Derives the plain-text fallback from rendered HTML: drops style/script blocks,
    /// turns block-level closers and line breaks into newlines, strips the remaining tags,
    /// decodes entities, and collapses leftover whitespace.
    /// </summary>
    public static string RenderText(string renderedHtml)
    {
        var text = StyleScriptRegex().Replace(renderedHtml, string.Empty);
        text = BlockBreakRegex().Replace(text, "\n");
        text = TagRegex().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = SpaceRunRegex().Replace(text, " ");
        text = NewlineRunRegex().Replace(text, "\n\n");
        return string.Join('\n', text.Split('\n').Select(line => line.Trim())).Trim();
    }

    [GeneratedRegex(@"<(style|script)[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleScriptRegex();

    [GeneratedRegex(@"<\s*(br\s*/?|/p|/div|/h[1-6]|/li|/tr|/table)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockBreakRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex SpaceRunRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex NewlineRunRegex();
}
