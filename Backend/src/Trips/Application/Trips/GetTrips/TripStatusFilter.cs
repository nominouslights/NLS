using System.Collections.Frozen;
using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.GetTrips;

/// <summary>
/// The status narrowing of <c>GET /api/trips</c>, parsed from its two mutually exclusive query
/// parameters. At most one of the two members is set; both null means "every status".
/// </summary>
/// <param name="Status"><c>?status=</c> — one exact status (the original filter).</param>
/// <param name="Statuses">
/// <c>?statuses=</c> — a set of statuses matched as one <c>IN</c> in a single query, so a caller
/// needing several statuses (the accruals report) reads one consistent snapshot rather than
/// stitching per-status calls together while trips move between them.
/// </param>
public sealed record TripStatusFilter(TripStatus? Status, IReadOnlyList<TripStatus>? Statuses)
{
    /// <summary>No status narrowing at all.</summary>
    public static readonly TripStatusFilter None = new(null, null);

    /// <summary>
    /// Parses the raw query values. Every name is matched against the enum's member NAMES,
    /// ordinally — never <see cref="Enum.TryParse{TEnum}(string?, out TEnum)"/>, which accepts
    /// <c>"2"</c> (and <c>IsDefined</c> then confirms it), so a numeric filter would bind by
    /// position and inserting a member would silently repoint it at a different status.
    /// <list type="bullet">
    /// <item><paramref name="status"/> null/whitespace = absent, exactly as it has always been.</item>
    /// <item><paramref name="statuses"/> null = absent; present but naming nothing
    /// (<c>""</c>, <c>" "</c>, <c>","</c>) is <see cref="TripErrors.EmptyStatusesFilter"/> —
    /// an empty set is never quietly read as "no filter".</item>
    /// <item>Each comma-separated segment of <paramref name="statuses"/> must be a member name
    /// exactly — no trimming, no case folding; an empty segment (<c>"Scheduled,,Invoiced"</c>,
    /// a trailing comma) is <see cref="TripErrors.InvalidStatusFilter"/>. Duplicates are harmless
    /// and collapsed.</item>
    /// <item>Both supplied is <see cref="TripErrors.ConflictingStatusFilters"/>, whether or not
    /// they agree.</item>
    /// </list>
    /// </summary>
    public static Result<TripStatusFilter> Parse(string? status, string? statuses)
    {
        var hasStatus = !string.IsNullOrWhiteSpace(status);

        if (statuses is null)
        {
            if (!hasStatus)
            {
                return Result.Success(None);
            }

            return IsStatusName(status!)
                ? Result.Success(new TripStatusFilter(Enum.Parse<TripStatus>(status!), null))
                : Result.Failure<TripStatusFilter>(TripErrors.InvalidStatusFilter);
        }

        if (hasStatus)
        {
            return Result.Failure<TripStatusFilter>(TripErrors.ConflictingStatusFilters);
        }

        var names = statuses.Split(',');
        if (names.All(string.IsNullOrWhiteSpace))
        {
            return Result.Failure<TripStatusFilter>(TripErrors.EmptyStatusesFilter);
        }

        var parsed = new List<TripStatus>(names.Length);
        foreach (var name in names)
        {
            if (!IsStatusName(name))
            {
                return Result.Failure<TripStatusFilter>(TripErrors.InvalidStatusFilter);
            }

            var value = Enum.Parse<TripStatus>(name);
            if (!parsed.Contains(value))
            {
                parsed.Add(value);
            }
        }

        return Result.Success(new TripStatusFilter(null, parsed));
    }

    // Built once: this runs per segment on every GET /api/trips. Ordinal, names only —
    // "2" or "scheduled" are not statuses, exactly as the original single-status check.
    private static readonly FrozenSet<string> StatusNames =
        Enum.GetNames<TripStatus>().ToFrozenSet(StringComparer.Ordinal);

    private static bool IsStatusName(string name) => StatusNames.Contains(name);
}
