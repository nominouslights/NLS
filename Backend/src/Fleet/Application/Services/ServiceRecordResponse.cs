namespace NorthernLink.Fleet.Application.Services;

/// <summary>
/// The Fleet module's public representation of a service record.
/// <paramref name="Category"/> is the ServiceCategory enum name (e.g. "InspectionFix").
/// </summary>
public sealed record ServiceRecordResponse(
    Guid Id,
    Guid VehicleId,
    string Number,
    DateTimeOffset Date,
    string PerformedBy,
    string Category,
    int OdometerKm,
    IReadOnlyList<string> ItemsChanged,
    string Reason,
    IReadOnlyList<ServicePartResponse> PartsUsed,
    decimal? LaborHours,
    decimal? CostCad,
    Guid? WorkOrderId,
    string? Notes,
    DateTimeOffset CreatedAtUtc);

/// <summary>One part consumed during a service.</summary>
public sealed record ServicePartResponse(string Sku, int Qty);
