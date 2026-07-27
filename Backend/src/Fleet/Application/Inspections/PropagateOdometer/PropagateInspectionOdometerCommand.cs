using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Inspections.PropagateOdometer;

/// <summary>
/// Same-module reaction to a newly entered inspection: advance the linked vehicle's odometer
/// from the inspection's reading. Keyed on the inspection id (from the journal row) — the
/// handler loads the inspection and resolves the vehicle. Post-trip (odometer-out) is what
/// moves the master odometer; a pre-trip (odometer-in) acts as a floor. Both go through the
/// monotonic <c>Vehicle.RecordOdometer</c>, so a non-monotonic reading is ignored, not an error.
/// </summary>
public sealed record PropagateInspectionOdometerCommand(
    Guid TenantId,
    Guid InspectionId) : ICommand;
