using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Completions.PropagateOdometer;

/// <summary>
/// Same-module reaction to a newly logged PM completion: advance the vehicle's odometer
/// from the completion's reading, exactly as inspections do
/// (<c>PropagateInspectionOdometerCommand</c>). Keyed on the completion id (from the
/// journal row) — the handler loads the completion and resolves the vehicle. The reading
/// goes through the monotonic <c>Vehicle.RecordOdometer</c>, so a historical (lower)
/// reading is ignored, not an error — only a completion ahead of the vehicle's current
/// reading moves the master odometer, closing the gap where a fresh completion far ahead of
/// a stale vehicle reading would deflate every other line's due math.
/// </summary>
public sealed record PropagatePmOdometerCommand(
    Guid TenantId,
    Guid CompletionId) : ICommand;
