using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections.ResolveDefect;

/// <summary>
/// Clears one open defect, addressed by <paramref name="InspectionId"/> + <paramref name="Item"/>
/// — there is no defect id, so the item travels in the request body rather than the path (it is
/// free text). Resolution is final: a second resolve of the same defect fails with
/// <c>DefectAlreadyResolved</c>. Every still-open defect for the same component on the vehicle's
/// other inspections is cleared with it (see <see cref="DuplicateDefects"/>), and the response
/// lists everything that was cleared.
/// </summary>
/// <param name="ResolvedBy">
/// Client-supplied, following the existing <c>EnteredBy</c> convention on this aggregate rather
/// than reading the JWT. Server-side attribution would be better and is a documented follow-up
/// across the whole module, not something to change on this one path.
/// </param>
public sealed record ResolveInspectionDefectCommand(
    Guid TenantId,
    Guid InspectionId,
    string Item,
    DefectResolutionReason Reason,
    string? Note,
    string? ResolvedBy) : ICommand<ResolveInspectionDefectResponse>;

/// <summary>
/// Body of a successful <c>POST /api/fleet/inspections/{id}/defects/resolve</c>: every defect the
/// call cleared. The addressed defect is ALWAYS first; the duplicates cleared alongside it follow,
/// oldest report first. A resolve with no duplicates returns a one-element list.
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
