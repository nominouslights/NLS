namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>
/// One defect found during an inspection. Persisted as jsonb inside the owning
/// <see cref="VehicleInspection"/>, which is why it has no id of its own: a defect is
/// addressed by <c>(InspectionId, Item)</c>, and <see cref="VehicleInspection.Enter"/> /
/// <see cref="VehicleInspection.Amend"/> reject a duplicate <see cref="Item"/> within one
/// inspection so that key stays unique going forward.
/// </summary>
public sealed record InspectionDefect
{
    public required string Item { get; init; }
    public InspectionDefectSeverity Severity { get; init; }
    public string? Note { get; init; }

    // Resolution. Absent from every jsonb row written before this change, which is exactly
    // the intended clean slate: the whole existing backlog reads back as unresolved, with no
    // data migration. Resolution is FINAL — there is no reopen. A fault that comes back is a
    // new defect on a later inspection, pointed at this one via RecurrenceOfInspectionId.
    public DefectResolutionReason? ResolutionReason { get; init; }
    public string? ResolutionNote { get; init; }
    public string? ResolvedBy { get; init; }
    public DateTimeOffset? ResolvedAtUtc { get; init; }
    public Guid? ResolvedByWorkOrderId { get; init; }

    /// <summary>
    /// The ACTIVE work order this defect is attached to, or null. Set when a work order is
    /// created with this defect on it; cleared when that work order resolves it (the id moves to
    /// <see cref="ResolvedByWorkOrderId"/>), defers it, or is cancelled. So a non-null value
    /// always means "on a work order that is still open" — a defect is on at most one.
    ///
    /// Not a resolution field: a defect resolved by hand while its work order is still open
    /// keeps this until that work order completes or is cancelled. Absent from every jsonb row
    /// written before per-defect links existed, which reads back as null — no data migration.
    /// </summary>
    public Guid? WorkOrderId { get; init; }

    /// <summary>Set when a dispatcher re-reports a defect that was resolved on an earlier inspection.</summary>
    public Guid? RecurrenceOfInspectionId { get; init; }

    public bool IsResolved => ResolvedAtUtc is not null;
}
