using System.Text;

namespace NorthernLink.Trips.Domain.BookeoImports;

/// <summary>
/// The normalizations the Bookeo import keys on. Kept in one place so a mapping saved from the
/// console and a value read from the spreadsheet can never disagree about what "the same" means.
/// </summary>
public static class BookeoText
{
    /// <summary>Trims and collapses internal whitespace runs to one space; null for blank.</summary>
    public static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var ch in value.Trim())
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    /// <summary>
    /// A Bookeo <c>Unit</c> cell or fleet unit number in comparable form — whitespace-collapsed
    /// and upper-cased, so "ford  transit 150" and "Ford Transit 150" are one unit. This is the
    /// form <c>bookeo_unit_mappings.unit_text</c> stores.
    /// </summary>
    public static string? NormalizeUnit(string? value) => Clean(value)?.ToUpperInvariant();

    /// <summary>A destination ("Leaf Rapids to Lynn Lake") in stored form: cleaned, case kept.</summary>
    public static string? NormalizeDestination(string? value) => Clean(value);

    /// <summary>Case-insensitive equality of two optional destinations (null matches only null).</summary>
    public static bool SameDestination(string? left, string? right) =>
        string.Equals(Clean(left), Clean(right), StringComparison.OrdinalIgnoreCase);

    /// <summary>Digits only — how phone numbers are compared ("204-555-0102" = "2045550102").</summary>
    public static string Digits(string? value) =>
        value is null ? string.Empty : new string(value.Where(char.IsAsciiDigit).ToArray());
}
