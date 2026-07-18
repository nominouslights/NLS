namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>Severity of a defect found during an inspection, as graded on the manifest.</summary>
public enum InspectionDefectSeverity
{
    Minor,
    Major,
    OutOfService,
}
