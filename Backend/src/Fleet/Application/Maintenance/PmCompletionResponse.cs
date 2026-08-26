namespace NorthernLink.Fleet.Application.Maintenance;

/// <summary>
/// One logged PM completion (the append-only per-unit service record).
/// <c>Kind</c> is "Item" or "Overhaul".
/// </summary>
public sealed record PmCompletionResponse(
    Guid Id,
    Guid VehicleId,
    Guid PlanId,
    string Code,
    string Kind,
    DateOnly PerformedAt,
    int OdometerKm,
    string PerformedBy,
    Guid? WorkOrderId,
    string? Measurement,
    string? Notes,
    DateTimeOffset CreatedAtUtc);
