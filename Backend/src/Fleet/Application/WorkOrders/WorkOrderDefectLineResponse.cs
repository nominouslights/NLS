using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Application.WorkOrders;

/// <summary>
/// One defect a work order was raised against. Keyed by <c>(InspectionId, Item)</c>, the same
/// address the defect has on its inspection. Severity is "Minor", "Major" or "OutOfService" and,
/// with <see cref="Note"/>, is the snapshot taken when the work order was created. Outcome is
/// "Repaired", "NoFaultFound" or "Deferred" — null until the work order completes.
/// </summary>
public sealed record WorkOrderDefectLineResponse(
    Guid InspectionId,
    string Item,
    string Severity,
    string? Note,
    string? Outcome,
    string? OutcomeNote)
{
    public static WorkOrderDefectLineResponse From(WorkOrderDefectLine line) => new(
        line.InspectionId,
        line.Item,
        line.Severity.ToString(),
        line.Note,
        line.Outcome?.ToString(),
        line.OutcomeNote);
}
