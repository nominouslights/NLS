namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>
/// One defect on one inspection — what a resolve reports back as cleared, and what the
/// duplicates preview lists.
/// </summary>
public sealed record DefectOnInspection(VehicleInspection Inspection, InspectionDefect Defect);

/// <summary>
/// The duplicate-defect rule. A known fault (a cracked mirror) is re-reported on every DVIR
/// until it is fixed, so one vehicle can carry several open defects for one component on
/// several inspections. When one of them is REPAIRED
/// (<see cref="DefectResolutionReasonExtensions.ClearsDuplicates"/>: a hand-recorded
/// PreviouslyRepaired, or a Repaired work-order line), the others are moot and can be cleared
/// with it. Any other resolution is a judgement about one report and clears nothing else.
///
/// A DUPLICATE of the repaired defect is a defect that is
/// <list type="bullet">
/// <item>on ANOTHER inspection (within one inspection Item is unique, so there are none),</item>
/// <item>of the same tenant and the same vehicle (<see cref="DefectVehicleScope"/>, the panel's
/// rule),</item>
/// <item>for the same component (<see cref="VehicleInspection.IsSameItem"/>),</item>
/// <item>still OPEN — resolved defects are never touched (resolution is final) — and</item>
/// <item>REPORTED BEFORE THE REPAIR: its inspection's <see cref="VehicleInspection.PerformedAt"/>
/// is strictly before the cutoff (<see cref="DefectRepairCutoff"/>). A later report may be the
/// fault coming back, so it stays open.</item>
/// </list>
/// <see cref="Find"/> is the read-only half (the preview endpoint and the cascade both call it,
/// so what was shown and what is cleared come from the same code); <see cref="Resolve"/> applies
/// it. Callers may narrow further — to the reports a dispatcher was shown, or away from a work
/// order's own lines — but never widen: everything passes through <see cref="Find"/>.
/// </summary>
public static class DuplicateDefects
{
    /// <summary>
    /// The open duplicates of <paramref name="source"/>'s defect <paramref name="item"/> among
    /// <paramref name="candidates"/>, oldest report first (<see cref="VehicleInspection.PerformedAt"/>,
    /// then entry order, then id). Changes nothing. The source defect may be open (a preview) or
    /// already resolved (the cascade). Candidates outside the tenant or the scope, the source
    /// itself, reports at or after <paramref name="reportedBefore"/>, and candidates with no open
    /// defect for the component are skipped, so the caller may pass a broad candidate set — the
    /// rule is applied here, not trusted to the query.
    /// </summary>
    public static IReadOnlyList<DefectOnInspection> Find(
        VehicleInspection source,
        string item,
        DefectVehicleScope scope,
        DateTimeOffset reportedBefore,
        IEnumerable<VehicleInspection> candidates)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(candidates);

        var found = new List<DefectOnInspection>();

        foreach (var candidate in candidates
            .DistinctBy(c => c.Id)
            .Where(c => c.Id != source.Id
                && c.TenantId == source.TenantId
                && c.PerformedAt < reportedBefore
                && scope.Includes(c))
            .OrderBy(c => c.PerformedAt)
            .ThenBy(c => c.CreatedAtUtc)
            .ThenBy(c => c.Id))
        {
            if (candidate.FindDefect(item) is { IsResolved: false } open)
            {
                found.Add(new DefectOnInspection(candidate, open));
            }
        }

        return found;
    }

    /// <summary>
    /// Clears the duplicates <see cref="Find"/> reports for <paramref name="source"/>'s defect
    /// <paramref name="item"/>, which must already be resolved FOR A REPAIR (anything else throws
    /// — the reason rule is the domain's, not the caller's). <paramref name="include"/> lets the
    /// caller narrow the set (null = all of it); it can never add a defect <see cref="Find"/> did
    /// not return. Each cleared defect takes the source's stamp verbatim
    /// (<see cref="VehicleInspection.ResolveAsDuplicateOf"/>).
    ///
    /// Returns what was cleared, oldest report first.
    /// </summary>
    public static IReadOnlyList<DefectOnInspection> Resolve(
        VehicleInspection source,
        string item,
        DefectVehicleScope scope,
        DateTimeOffset reportedBefore,
        IEnumerable<VehicleInspection> candidates,
        Func<DefectOnInspection, bool>? include = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var repaired = source.FindDefect(item);
        if (repaired is null || !repaired.IsResolved)
        {
            throw new InvalidOperationException(
                "Duplicates are resolved only after the source defect itself has been resolved.");
        }

        if (repaired.ResolutionReason is not { } reason || !reason.ClearsDuplicates())
        {
            throw new InvalidOperationException(
                $"Only a repair clears duplicates; a {repaired.ResolutionReason} resolution is a judgement about one report.");
        }

        var cleared = new List<DefectOnInspection>();

        foreach (var duplicate in Find(source, item, scope, reportedBefore, candidates))
        {
            if (include is not null && !include(duplicate))
            {
                continue;
            }

            if (duplicate.Inspection.ResolveAsDuplicateOf(source.Id, repaired) is { } resolved)
            {
                cleared.Add(new DefectOnInspection(duplicate.Inspection, resolved));
            }
        }

        return cleared;
    }
}
