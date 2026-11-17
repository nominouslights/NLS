namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>
/// One defect on one inspection — what a resolve reports back as cleared.
/// </summary>
public sealed record DefectOnInspection(VehicleInspection Inspection, InspectionDefect Defect);

/// <summary>
/// The duplicate-defect rule. A known fault (a cracked mirror) is re-reported on every DVIR
/// until it is fixed, so one vehicle can carry several open defects for one component on
/// several inspections. Resolving any one of them — by hand, or by a work-order line that ends
/// Repaired / NoFaultFound — resolves every other one too.
///
/// A DUPLICATE of a just-resolved defect is a defect that is
/// <list type="bullet">
/// <item>on ANOTHER inspection (within one inspection Item is unique, so there are none),</item>
/// <item>of the same tenant and the same vehicle (<see cref="DefectVehicleScope"/>),</item>
/// <item>for the same component — the same Item key, trimmed and case-insensitive, and</item>
/// <item>still OPEN at that moment. Resolved defects are never touched (resolution is final),
/// and one reported afterwards is a new defect — a recurrence, not a duplicate.</item>
/// </list>
/// Each duplicate takes the source defect's stamp verbatim plus
/// <see cref="InspectionDefect.ResolvedWithInspectionId"/>; see
/// <see cref="VehicleInspection.ResolveAsDuplicateOf"/> for the work-order-link rule.
/// </summary>
public static class DuplicateDefects
{
    /// <summary>
    /// Resolves, on every candidate inspection in <paramref name="scope"/>, the open duplicate of
    /// <paramref name="source"/>'s defect <paramref name="item"/>, which must already be
    /// resolved. Candidates outside the scope or the tenant, the source itself, and candidates
    /// with no open defect for the component are skipped untouched (no change, no event), so the
    /// caller may pass a broad candidate set — the rule is applied here, not trusted to the query.
    ///
    /// Returns what was cleared, oldest report first (<see cref="VehicleInspection.PerformedAt"/>,
    /// then entry order).
    /// </summary>
    public static IReadOnlyList<DefectOnInspection> Resolve(
        VehicleInspection source,
        string item,
        DefectVehicleScope scope,
        IEnumerable<VehicleInspection> candidates)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(candidates);

        var resolved = source.FindDefect(item);
        if (resolved is null || !resolved.IsResolved)
        {
            throw new InvalidOperationException(
                "Duplicates are resolved only after the source defect itself has been resolved.");
        }

        var cleared = new List<DefectOnInspection>();

        foreach (var candidate in candidates
            .DistinctBy(c => c.Id)
            .Where(c => c.Id != source.Id && c.TenantId == source.TenantId && scope.Includes(c))
            .OrderBy(c => c.PerformedAt)
            .ThenBy(c => c.CreatedAtUtc)
            .ThenBy(c => c.Id))
        {
            if (candidate.ResolveAsDuplicateOf(source.Id, resolved) is { } duplicate)
            {
                cleared.Add(new DefectOnInspection(candidate, duplicate));
            }
        }

        return cleared;
    }
}
