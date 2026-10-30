namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>
/// How one checklist row of paper form NL-PTI-01 was answered: OK, Defect, or N/A. The paper
/// form has three boxes, so the platform must too — before this existed a row was a
/// <see cref="InspectionChecklistItem.Passed"/> bool and every "does not apply to this unit"
/// answer was flattened into one of the other two on submit.
/// </summary>
public enum ChecklistItemState
{
    Ok,
    Defect,
    NotApplicable,
}
