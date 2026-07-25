namespace NorthernLink.Fleet.Domain.Services;

/// <summary>Category of a maintenance service record. "InspectionFix" ↔ "Inspection Fix" on the UI.</summary>
public enum ServiceCategory
{
    Preventive,
    Repair,
    InspectionFix,
    Recall,
}
