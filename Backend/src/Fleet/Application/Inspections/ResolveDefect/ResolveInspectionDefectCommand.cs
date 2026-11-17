using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections.ResolveDefect;

/// <summary>
/// Clears one open defect, addressed by <paramref name="InspectionId"/> + <paramref name="Item"/>
/// — there is no defect id, so the item travels in the request body rather than the path (it is
/// free text). Resolution is final: a second resolve of the same defect fails with
/// <c>DefectAlreadyResolved</c>.
/// </summary>
/// <param name="ResolvedBy">
/// Client-supplied, following the existing <c>EnteredBy</c> convention on this aggregate rather
/// than reading the JWT. Server-side attribution would be better and is a documented follow-up
/// across the whole module, not something to change on this one path.
/// </param>
/// <param name="ClearDuplicates">
/// The other open reports of the same component the dispatcher was SHOWN (by
/// <c>GET …/defects/duplicates</c>) and chose to clear with this one. Only a repair
/// (<see cref="DefectResolutionReason.PreviouslyRepaired"/>) may carry a non-empty list —
/// anything else fails with <c>DuplicatesOnlyClearedByRepair</c>. At write time only the listed
/// reports that are STILL valid duplicates are cleared (the intersection with
/// <see cref="DuplicateDefects.Find"/>); one that no longer qualifies is skipped silently. Null or
/// empty clears no duplicates.
/// </param>
public sealed record ResolveInspectionDefectCommand(
    Guid TenantId,
    Guid InspectionId,
    string Item,
    DefectResolutionReason Reason,
    string? Note,
    string? ResolvedBy,
    IReadOnlyList<DuplicateDefectReference>? ClearDuplicates = null) : ICommand<ResolveInspectionDefectResponse>;

/// <summary>
/// One other report to clear alongside the addressed defect, keyed the way every defect is:
/// <c>(InspectionId, Item)</c>. <see cref="Item"/> matches by the domain's item rule (trimmed,
/// case-insensitive).
/// </summary>
public sealed record DuplicateDefectReference(Guid InspectionId, string? Item);

/// <summary>
/// Body of a successful <c>POST /api/fleet/inspections/{id}/defects/resolve</c>: every defect the
/// call ACTUALLY cleared. The addressed defect is always first; the duplicates cleared alongside
/// it follow, oldest report first. A resolve that cleared no duplicates — none requested, none
/// still valid, or the duplicate save lost every retry — returns a one-element list.
/// </summary>
public sealed record ResolveInspectionDefectResponse(IReadOnlyList<ResolvedDefectResponse> Resolved);

/// <summary>One cleared defect, keyed the way every defect is: <c>(InspectionId, Item)</c>.</summary>
/// <param name="Item">The defect's Item as stored on that inspection (its own spelling and case).</param>
/// <param name="TripNumber">The trip the reporting inspection belongs to; null for a standalone entry.</param>
/// <param name="Severity">"Minor", "Major", or "OutOfService".</param>
public sealed record ResolvedDefectResponse(
    Guid InspectionId,
    string Item,
    string? TripNumber,
    string Severity)
{
    public static ResolvedDefectResponse From(VehicleInspection inspection, InspectionDefect defect) => new(
        inspection.Id,
        defect.Item,
        inspection.TripNumber,
        defect.Severity.ToString());
}
