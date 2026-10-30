using System.Text.RegularExpressions;
using NorthernLink.Trips.Domain.BookeoImports;

namespace NorthernLink.Trips.Application.BookeoImports.Parsing;

/// <summary>
/// Parses Bookeo's <c>Participants (details)</c> cell — one block per passenger, blocks separated
/// by blank lines:
/// <code>
/// ### Lynn Lake Residents 1:
/// Alex Sample
/// alex@example.test
/// 2045550101 (mobile)
/// </code>
/// The name line is required; email and phone are each optional (in real exports usually only
/// passenger 1 has them). A block with no name yields no passenger — the planner tops a short
/// list up with placeholders and warns.
/// </summary>
public static partial class BookeoParticipantsDetailsParser
{
    public static IReadOnlyList<BookeoParsedPassenger> Parse(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return [];
        }

        var passengers = new List<BookeoParsedPassenger>();
        string? category = null;
        var index = 0;
        var body = new List<string>();

        void Flush()
        {
            if (category is not null && body.Count > 0)
            {
                var name = BookeoText.Clean(body[0]);
                if (name is not null)
                {
                    string? email = null;
                    string? phone = null;
                    foreach (var line in body.Skip(1))
                    {
                        if (email is null && line.Contains('@', StringComparison.Ordinal) && !line.Contains(' ', StringComparison.Ordinal))
                        {
                            email = line;
                        }
                        else if (phone is null && BookeoText.Digits(line).Length >= 7)
                        {
                            phone = BookeoText.Clean(PhoneSuffix().Replace(line, string.Empty));
                        }
                    }

                    passengers.Add(new BookeoParsedPassenger(category, index, name, email, phone));
                }
            }

            body.Clear();
        }

        foreach (var raw in details.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Trim();
            var header = BlockHeader().Match(line);
            if (header.Success)
            {
                Flush();
                category = BookeoText.Clean(header.Groups["category"].Value) ?? string.Empty;
                index = int.TryParse(header.Groups["n"].Value, out var n) ? n : passengers.Count + 1;
                continue;
            }

            if (line.Length > 0 && category is not null)
            {
                body.Add(line);
            }
        }

        Flush();
        return passengers;
    }

    [GeneratedRegex(@"^###\s*(?<category>.+?)\s+(?<n>\d+)\s*:?\s*$")]
    private static partial Regex BlockHeader();

    [GeneratedRegex(@"\s*\([^)]*\)\s*$")]
    private static partial Regex PhoneSuffix();
}
