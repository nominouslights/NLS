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

    /// <summary>
    /// Set only by work-order completion: a mechanic inspected the reported fault under a work
    /// order and found nothing wrong. Distinct from <see cref="ReportedInError"/>, which is a
    /// dispatcher's call made without anyone touching the truck.
    /// </summary>
    NoFaultFound,
}

/// <summary>Rules attached to a <see cref="DefectResolutionReason"/>.</summary>
public static class DefectResolutionReasonExtensions
{
    /// <summary>
    /// THE duplicate-clearing rule: true only for a resolution that asserts the COMPONENT was
    /// fixed — <see cref="DefectResolutionReason.PreviouslyRepaired"/> (by hand) and
    /// <see cref="DefectResolutionReason.RepairedUnderWorkOrder"/> (a Repaired work-order line).
    /// A repair fixes the part, so every other still-open report of that part made before the
    /// repair is moot. <see cref="DefectResolutionReason.ReportedInError"/>,
    /// <see cref="DefectResolutionReason.AcceptedMonitoring"/> and
    /// <see cref="DefectResolutionReason.NoFaultFound"/> are judgements about ONE report — that
    /// this driver was wrong, that this report is being run with, that a mechanic found nothing
    /// on this occasion — and say nothing about the others, so they never cascade.
    /// </summary>
    public static bool ClearsDuplicates(this DefectResolutionReason reason) =>
        reason is DefectResolutionReason.PreviouslyRepaired or DefectResolutionReason.RepairedUnderWorkOrder;
}
