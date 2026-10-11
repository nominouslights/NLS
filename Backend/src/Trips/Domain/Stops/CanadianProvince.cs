namespace NorthernLink.Trips.Domain.Stops;

/// <summary>
/// The thirteen Canadian provinces and territories as two-letter postal codes, with a
/// pragmatic normaliser for what dispatchers actually type. A province travels everywhere in
/// the module as its code ("MB"): on a catalog <see cref="Address"/>, on every route-stop
/// snapshot, and on a trip's free-form corridor endpoints. Codes are what
/// <c>OperatingArea</c> compares against, so a free-text spelling can never slip past the
/// Manitoba-only gate by being spelled differently.
/// </summary>
public static class CanadianProvince
{
    /// <summary>Manitoba — the one province Northern Link operates in, and the default wherever a province is absent.</summary>
    public const string Manitoba = "MB";

    /// <summary>Every code, in the conventional west-to-east, provinces-then-territories order.</summary>
    public static readonly IReadOnlyList<string> Codes =
    [
        "BC", "AB", "SK", "MB", "ON", "QC", "NB", "NS", "PE", "NL", "YT", "NT", "NU",
    ];

    private static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["BC"] = "British Columbia",
        ["AB"] = "Alberta",
        ["SK"] = "Saskatchewan",
        ["MB"] = "Manitoba",
        ["ON"] = "Ontario",
        ["QC"] = "Quebec",
        ["NB"] = "New Brunswick",
        ["NS"] = "Nova Scotia",
        ["PE"] = "Prince Edward Island",
        ["NL"] = "Newfoundland and Labrador",
        ["YT"] = "Yukon",
        ["NT"] = "Northwest Territories",
        ["NU"] = "Nunavut",
    };

    /// <summary>
    /// Everything <see cref="Normalize"/> recognises, keyed by its folded form (see
    /// <see cref="Fold"/>): the codes, the English and French names, and the traditional
    /// abbreviations ("Man.", "Sask.", "P.E.I.", "Nfld.", …). Deliberately a closed list —
    /// an unknown spelling is refused rather than guessed.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Aliases = BuildAliases();

    /// <summary>The province's English name for a code, or the code itself when it is not one of the thirteen.</summary>
    public static string NameOf(string code) =>
        Names.TryGetValue(code, out var name) ? name : code;

    /// <summary>Whether <paramref name="code"/> is exactly one of the thirteen two-letter codes (case-sensitive — the stored form).</summary>
    public static bool IsCode(string? code) =>
        code is not null && Names.ContainsKey(code);

    /// <summary>
    /// The two-letter code for whatever was typed — "Manitoba", "MB", "mb", "Man." all give
    /// "MB" — or null when the value is blank or not a recognised province. Trimmed and
    /// case-insensitive; internal whitespace and punctuation ("P.E.I.", "Prince  Edward Island")
    /// are ignored.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Aliases.TryGetValue(Fold(value), out var code) ? code : null;
    }

    /// <summary>
    /// <see cref="Normalize"/> with the module's default: a blank value is Manitoba. Returns
    /// false only when a non-blank value is not a recognised province, with
    /// <paramref name="code"/> then left null.
    /// </summary>
    public static bool TryNormalizeOrDefault(string? value, out string? code)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            code = Manitoba;
            return true;
        }

        code = Normalize(value);
        return code is not null;
    }

    /// <summary>Lower-cases and strips every character that is not a letter, so "P.E.I." and "pei" fold alike.</summary>
    private static string Fold(string value) =>
        new([.. value.Where(char.IsLetter).Select(char.ToLowerInvariant)]);

    private static Dictionary<string, string> BuildAliases()
    {
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);

        void Add(string code, params string[] spellings)
        {
            aliases[Fold(code)] = code;
            aliases[Fold(Names[code])] = code;
            foreach (var spelling in spellings)
            {
                aliases[Fold(spelling)] = code;
            }
        }

        Add("BC", "B.C.", "Colombie-Britannique");
        Add("AB", "Alta.", "Alta");
        Add("SK", "Sask.", "Sask");
        Add("MB", "Man.", "Man");
        Add("ON", "Ont.", "Ont");
        Add("QC", "Que.", "Que", "Québec", "PQ", "P.Q.");
        Add("NB", "N.B.", "Nouveau-Brunswick");
        Add("NS", "N.S.", "Nouvelle-Écosse");
        Add("PE", "P.E.I.", "PEI", "Île-du-Prince-Édouard");
        Add("NL", "N.L.", "Nfld.", "Nfld", "Newfoundland", "Nfld. & Lab.", "Newfoundland & Labrador", "Terre-Neuve-et-Labrador");
        Add("YT", "Y.T.", "Yukon Territory", "YK");
        Add("NT", "N.W.T.", "NWT", "Territoires du Nord-Ouest");
        Add("NU", "Nvt.", "Nvt");

        return aliases;
    }
}
