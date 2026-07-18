namespace NorthernLink.Fleet.Domain.WorkOrders;

/// <summary>What prompted a work order. Enum names map to UI labels (e.g. "PreTripInspection" → "Pre-Trip Inspection").</summary>
public enum WorkOrderSource
{
    Manual,
    PreTripInspection,
    PostTripInspection,
    DtcAlert,
    PmReminder,
}
