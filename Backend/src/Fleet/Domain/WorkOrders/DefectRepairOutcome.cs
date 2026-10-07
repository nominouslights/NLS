namespace NorthernLink.Fleet.Domain.WorkOrders;

/// <summary>
/// What the mechanic found for one defect a work order was raised against, recorded at
/// completion on its <see cref="WorkOrderDefectLine"/>. Persisted as its name inside the
/// work order's <c>defects</c> jsonb, never as an int.
/// </summary>
public enum DefectRepairOutcome
{
    /// <summary>Fixed. The defect resolves as <c>RepairedUnderWorkOrder</c>.</summary>
    Repaired,

    /// <summary>Inspected and nothing was wrong. The defect resolves as <c>NoFaultFound</c>.</summary>
    NoFaultFound,

    /// <summary>
    /// Not done on this work order. The defect stays OPEN and is released so a later work order
    /// can take it. Needs a note, and an OutOfService defect can never be deferred — a truck
    /// with an out-of-service fault is not allowed to keep running with it.
    /// </summary>
    Deferred,
}
