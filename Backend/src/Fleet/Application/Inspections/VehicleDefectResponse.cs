namespace NorthernLink.Fleet.Application.Inspections;

/// <summary>
/// One defect on one vehicle, flattened out of its DVIR — the shape the dispatcher's defects
/// panel consumes. A vehicle's defects are not an aggregate of their own: this row is derived on
/// read from the inspection that reported it, plus the work order that inspection generated.
///
/// <para><b>There is no defect id.</b> A defect lives inside its inspection's jsonb, so callers
/// key a row on <c>(InspectionId, Item)</c> — that pair is also what
/// <c>POST /api/fleet/inspections/{id}/defects/resolve</c> addresses. Entry and amendment reject
/// a duplicate <c>Item</c> within one inspection so the pair stays unique going forward;
/// pre-existing duplicates in historical data resolve together, which is the accepted cost of
/// having no id to backfill.</para>
///
/// <para><b>Resolution is final — there is no reopen.</b> All five resolution fields are null
/// while the defect is open. A fault that comes back is a NEW defect on a later inspection, and
/// <see cref="Recurrence"/> points at the earlier one that was cleared, so the original record
/// stays intact. Completing a work order resolves EVERY unresolved defect on the inspection that
/// generated it (one work order per inspection is the aggregate's rule), all of them attributed
/// to that work order.</para>
///
/// <para><b>No <c>since</c> or <c>limit</c> parameter, deliberately.</b> The most dangerous row
/// is the oldest one — an out-of-service defect from fourteen months ago with no work order is
/// exactly what must not be hidden — and a server-side cutoff would let the four surfaces
/// disagree about the same truck's count. Noise is handled by resolution (the backlog shrinks as
/// dispatchers work it) and by presentation. If volume ever demands a cutoff it belongs in the
/// inspection query in the database, before materialization, not here.</para>
/// </summary>
/// <param name="InspectionId">The DVIR that reported it — half of the row's key.</param>
/// <param name="ReportedAt">
/// The inspection's <c>PerformedAt</c> — when the driver found the fault, not when it was typed in.
/// </param>
/// <param name="Item">The other half of the row's key. Free text, as graded on the DVIR.</param>
/// <param name="Severity">"Minor", "Major", or "OutOfService".</param>
/// <param name="WorkOrderId">
/// The inspection's work order whatever its status — "repair underway" context, present on open
/// and resolved rows alike. A cancelled work order still shows here: the dispatcher should see
/// that a repair was attempted and called off.
/// </param>
/// <param name="ResolutionReason">A <c>DefectResolutionReason</c> name, or null while open.</param>
/// <param name="Recurrence">
/// Derived per read, never stored: the most recent earlier resolution of the same item on the
/// same vehicle. Null when this fault has no cleared history.
/// </param>
public sealed record VehicleDefectResponse(
    Guid InspectionId,
    Guid? VehicleId,
    string Unit,
    string InspectionType,
    string? TripNumber,
    string DriverName,
    DateTimeOffset ReportedAt,
    string Item,
    string Severity,
    string? Note,
    Guid? WorkOrderId,
    string? WorkOrderNumber,
    string? WorkOrderStatus,
    string? ResolutionReason,
    string? ResolutionNote,
    string? ResolvedBy,
    DateTimeOffset? ResolvedAtUtc,
    Guid? ResolvedByWorkOrderId,
    PreviousResolutionResponse? Recurrence);

/// <summary>
/// The earlier, already-cleared report of the same fault that this defect supersedes — the
/// "this has come back" citation. Derived on read by matching <c>Item</c> (trimmed,
/// case-insensitive) on the same vehicle with a strictly earlier <c>PerformedAt</c>; the most
/// recent match wins. Nothing is stored, so nothing can go out of sync.
/// </summary>
/// <param name="Explicit">
/// True when the open defect carries a <c>RecurrenceOfInspectionId</c> — a dispatcher deliberately
/// re-reported it rather than waiting for the next DVIR. False when the link was inferred from
/// the item name alone.
/// </param>
public sealed record PreviousResolutionResponse(
    Guid InspectionId,
    DateTimeOffset ReportedAt,
    DateTimeOffset ResolvedAtUtc,
    string ResolutionReason,
    string? WorkOrderNumber,
    bool Explicit);
