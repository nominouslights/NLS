using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Inspections.GetDuplicates;

/// <summary>
/// The preview behind "also clear N other reports": the open duplicates of the defect addressed
/// by <paramref name="InspectionId"/> + <paramref name="Item"/> that a repair-type resolve of it
/// (<c>PreviouslyRepaired</c>) would clear RIGHT NOW. Read from the WRITE model through the same
/// scope, query and rule as the cascade, so the list a dispatcher confirms is the list the
/// resolve can clear (it then clears only what was shown and still qualifies).
/// </summary>
public sealed record GetDefectDuplicatesQuery(Guid TenantId, Guid InspectionId, string Item)
    : IQuery<DefectDuplicatesResponse>;

/// <summary>
/// Body of <c>GET /api/fleet/inspections/{id}/defects/duplicates?item=</c>. Oldest report first.
/// Empty when the addressed defect is already resolved or has no open duplicates.
/// </summary>
public sealed record DefectDuplicatesResponse(IReadOnlyList<DefectDuplicateResponse> Duplicates);

/// <summary>One open duplicate, keyed the way every defect is: <c>(InspectionId, Item)</c>.</summary>
/// <param name="Item">The duplicate's Item as stored on its own inspection (its own spelling and case).</param>
/// <param name="TripNumber">The trip the reporting inspection belongs to; null for a standalone entry.</param>
/// <param name="Severity">"Minor", "Major", or "OutOfService" — the duplicate's own grading.</param>
/// <param name="ReportedAt">The reporting inspection's PerformedAt.</param>
public sealed record DefectDuplicateResponse(
    Guid InspectionId,
    string Item,
    string? TripNumber,
    string Severity,
    DateTimeOffset ReportedAt);
