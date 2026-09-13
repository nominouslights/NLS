namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>
/// Why a defect stopped being an open fault. Resolution is FINAL — there is no reopen, so a
/// fault that comes back is a new defect on a later inspection rather than a status flip here
/// (see <see cref="InspectionDefect.RecurrenceOfInspectionId"/>). Persisted as its name inside
/// the defects jsonb, never as an int.
/// </summary>
public enum DefectResolutionReason
{
    /// <summary>Set only by work-order completion — a mechanic actually touched the truck.</summary>
    RepairedUnderWorkOrder,

    /// <summary>The repair happened outside the work-order flow (a road fix, another shop's invoice).</summary>
    PreviouslyRepaired,

    /// <summary>The DVIR was wrong: the fault was never there.</summary>
    ReportedInError,

    /// <summary>Real, known, and deliberately being run with — watched rather than repaired.</summary>
    AcceptedMonitoring,
}
