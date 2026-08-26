using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Completions.Log;

/// <summary>
/// Records that a plan item or overhaul (<paramref name="Code"/>, <paramref name="Kind"/>)
/// was completed on a vehicle. The plan id comes from the vehicle's current assignment,
/// never from the caller. Returns the new completion's id.
/// </summary>
public sealed record LogPmCompletionCommand(
    Guid TenantId,
    Guid VehicleId,
    string Code,
    PmEntryKind Kind,
    DateOnly PerformedAt,
    int OdometerKm,
    string PerformedBy,
    Guid? WorkOrderId,
    string? Measurement,
    string? Notes) : ICommand<Guid>;
