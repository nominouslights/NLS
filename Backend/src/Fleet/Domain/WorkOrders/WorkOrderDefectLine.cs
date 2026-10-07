using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Domain.WorkOrders;

/// <summary>
/// One inspection defect a work order was raised against — the real per-defect link that
/// replaces matching free-text line items. Persisted as jsonb inside the owning
/// <see cref="WorkOrder"/> (column <c>defects</c>), so like the defect it points at it has no id
/// of its own: it is keyed by <c>(InspectionId, Item)</c>, the same address the defect has on
/// its inspection.
///
/// <see cref="Severity"/> and <see cref="Note"/> are a SNAPSHOT taken when the work order was
/// created — the mechanic works from what the work order said, and a later amendment of the
/// inspection does not rewrite the work order. <see cref="Outcome"/>/<see cref="OutcomeNote"/>
/// are null until the work order completes.
/// </summary>
public sealed record WorkOrderDefectLine
{
    public Guid InspectionId { get; init; }
    public required string Item { get; init; }
    public InspectionDefectSeverity Severity { get; init; }
    public string? Note { get; init; }
    public DefectRepairOutcome? Outcome { get; init; }
    public string? OutcomeNote { get; init; }

    /// <summary>True when this line addresses <paramref name="item"/> on <paramref name="inspectionId"/> (trimmed, case-insensitive).</summary>
    public bool Addresses(Guid inspectionId, string? item) =>
        InspectionId == inspectionId
        && string.Equals(Item.Trim(), item?.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>The outcome submitted for one <see cref="WorkOrderDefectLine"/> when the work order completes.</summary>
public sealed record WorkOrderDefectOutcome(
    Guid InspectionId,
    string Item,
    DefectRepairOutcome Outcome,
    string? Note);
