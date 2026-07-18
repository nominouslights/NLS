namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>
/// Overall inspection outcome, derived from the defects: no defects → Pass; only Minor
/// defects → PassWithDefects; any Major or OutOfService defect → Fail.
/// </summary>
public enum InspectionResult
{
    Pass,
    PassWithDefects,
    Fail,
}
