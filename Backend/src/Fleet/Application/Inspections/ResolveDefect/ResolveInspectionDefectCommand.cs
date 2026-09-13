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
public sealed record ResolveInspectionDefectCommand(
    Guid TenantId,
    Guid InspectionId,
    string Item,
    DefectResolutionReason Reason,
    string? Note,
    string? ResolvedBy) : ICommand;
