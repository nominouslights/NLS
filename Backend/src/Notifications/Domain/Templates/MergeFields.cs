using System.Text.RegularExpressions;

namespace NorthernLink.Notifications.Domain.Templates;

/// <summary>
/// The canonical merge-field vocabulary for email templates — the single source of truth the
/// frontend's <c>MERGE_FIELDS</c> list mirrors. Tokens are written <c>{{PascalCase}}</c>
/// (whitespace inside the braces tolerated, case-sensitive). Template save validates against
/// this set so a typo fails loudly at authoring time instead of rendering an empty hole in a
/// passenger's email.
/// </summary>
public static partial class MergeFields
{
    /// <summary>The passenger's display name.</summary>
    public const string PassengerName = "PassengerName";

    /// <summary>The trip's formatted service date.</summary>
    public const string TripDate = "TripDate";

    /// <summary>The trip's pickup-window start time.</summary>
    public const string PickupTime = "PickupTime";

    /// <summary>The trip's route name.</summary>
    public const string Route = "Route";

    /// <summary>The passenger's pickup stop (falls back to the trip origin upstream).</summary>
    public const string PickupStop = "PickupStop";

    /// <summary>The passenger's pickup stop address (opaque string supplied by the caller).</summary>
    public const string PickupAddress = "PickupAddress";

    /// <summary>The passenger's dropoff stop (falls back to the trip destination upstream).</summary>
    public const string DropoffStop = "DropoffStop";

    /// <summary>The passenger's dropoff/final stop address (opaque string supplied by the caller).</summary>
    public const string DropoffStopAddress = "DropoffStopAddress";

    /// <summary>The trip's human-readable trip number.</summary>
    public const string TripNumber = "TripNumber";

    /// <summary>The trip's client organization name (empty when the trip has none).</summary>
    public const string ClientName = "ClientName";

    /// <summary>Every valid token name, in display order.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        PassengerName, TripDate, PickupTime, Route, PickupStop, PickupAddress, DropoffStop, DropoffStopAddress, TripNumber, ClientName,
    ];

    private static readonly HashSet<string> AllSet = new(All, StringComparer.Ordinal);

    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}")]
    private static partial Regex TokenRegex();

    /// <summary>
    /// Token names referenced by <paramref name="text"/> that are not in the canonical set.
    /// Empty when every token is valid (or the text has none).
    /// </summary>
    public static IReadOnlyList<string> UnknownTokensIn(string text) =>
        TokenRegex().Matches(text)
            .Select(match => match.Groups[1].Value)
            .Where(token => !AllSet.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Replaces every <c>{{Token}}</c> in <paramref name="template"/> with
    /// <paramref name="encode"/> applied to the token's value. A token with no value in
    /// <paramref name="values"/> renders as an empty string — a missing value must never
    /// leak the raw token into a passenger-facing email.
    /// </summary>
    public static string Substitute(
        string template,
        IReadOnlyDictionary<string, string> values,
        Func<string, string> encode) =>
        TokenRegex().Replace(template, match =>
            values.TryGetValue(match.Groups[1].Value, out var value) ? encode(value) : string.Empty);
}
