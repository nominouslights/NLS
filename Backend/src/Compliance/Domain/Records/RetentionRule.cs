namespace NorthernLink.Compliance.Domain.Records;

/// <summary>One arm of a retention rule: keep until <see cref="Anchor"/> + <see cref="OffsetMonths"/>.</summary>
public sealed record RetentionTerm(AnchorKind Anchor, int OffsetMonths);

/// <summary>
/// How long a record type is kept: a list of terms combined as "whichever is later", or
/// permanent (no terms). <c>R10</c> "2 years AND 6 months after the vehicle leaves control" is
/// <c>LaterOf(RecordDate+24, VehicleLeftControl+6)</c>; <c>R9</c> "month created + 6 months" is
/// <c>After(MonthEnd, 6)</c>; <c>R21</c> is <see cref="Permanent"/>. Serialized as-is into the
/// rule set's retention section, so the numbers people read are the numbers the calculator uses.
/// </summary>
public sealed record RetentionRule(IReadOnlyList<RetentionTerm> Terms)
{
    public static RetentionRule Permanent { get; } = new([]);

    public bool IsPermanent => Terms.Count == 0;

    public static RetentionRule After(AnchorKind anchor, int offsetMonths) => new([new RetentionTerm(anchor, offsetMonths)]);

    public static RetentionRule LaterOf(params RetentionTerm[] terms)
    {
        if (terms.Length < 2)
        {
            throw new ArgumentException("A later-of rule needs at least two terms.", nameof(terms));
        }

        return new RetentionRule(terms);
    }

    /// <summary>The distinct anchors this rule needs before it can compute a date.</summary>
    public IReadOnlyList<AnchorKind> Anchors => Terms.Select(t => t.Anchor).Distinct().ToList();
}

/// <summary>What <see cref="RetentionCalculator"/> decided for one record.</summary>
/// <param name="RetainUntil">The last day the record must be kept; null while pending or permanent.</param>
/// <param name="Pending">True when an anchor the rule needs is not yet known (the record stays Active).</param>
/// <param name="Permanent">True for a never-disposable record type.</param>
public sealed record RetentionOutcome(DateOnly? RetainUntil, bool Pending, bool Permanent);

/// <summary>
/// Pure arithmetic from a <see cref="RetentionRule"/> and the record's known anchor dates to a
/// retain-until date. No clock, no I/O — the worker and the aggregate both call this, so the
/// unit tests here are the proof of every row in the s. 11 table.
/// <para>
/// A later-of rule is pending while <em>any</em> of its anchors is unknown: R10 cannot settle while
/// the vehicle is still in the fleet, R19 cannot settle while a claim is open. That is correct —
/// the record is not disposable until the later arm is known — and the calculator says so with
/// <see cref="RetentionOutcome.Pending"/> rather than guessing a lower bound.
/// </para>
/// </summary>
public static class RetentionCalculator
{
    public static RetentionOutcome Compute(RetentionRule rule, IReadOnlyDictionary<AnchorKind, DateOnly> anchors)
    {
        if (rule.IsPermanent)
        {
            return new RetentionOutcome(null, Pending: false, Permanent: true);
        }

        DateOnly? latest = null;
        foreach (var term in rule.Terms)
        {
            if (!anchors.TryGetValue(term.Anchor, out var anchorDate))
            {
                return new RetentionOutcome(null, Pending: true, Permanent: false);
            }

            var until = anchorDate.AddMonths(term.OffsetMonths);
            if (latest is null || until > latest.Value)
            {
                latest = until;
            }
        }

        return new RetentionOutcome(latest, Pending: false, Permanent: false);
    }

    /// <summary>
    /// Fills in anchors that follow from others: <see cref="AnchorKind.MonthEnd"/> is the last day
    /// of <see cref="AnchorKind.RecordDate"/>'s month when a producer sent only the record date.
    /// Nothing else is derived — ReceivedDate, RepairDate, EmploymentEnd and the rest are facts
    /// only the producer knows.
    /// </summary>
    public static IReadOnlyDictionary<AnchorKind, DateOnly> WithDerivedAnchors(IReadOnlyDictionary<AnchorKind, DateOnly> anchors)
    {
        if (anchors.ContainsKey(AnchorKind.MonthEnd) || !anchors.TryGetValue(AnchorKind.RecordDate, out var recordDate))
        {
            return anchors;
        }

        var withMonthEnd = new Dictionary<AnchorKind, DateOnly>(anchors)
        {
            [AnchorKind.MonthEnd] = new DateOnly(recordDate.Year, recordDate.Month, DateTime.DaysInMonth(recordDate.Year, recordDate.Month)),
        };
        return withMonthEnd;
    }
}
