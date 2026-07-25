using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Domain.Services;

namespace NorthernLink.Fleet.Application.Services.Add;

/// <summary>Logs a maintenance service record against a vehicle. Returns the new record's id.</summary>
public sealed record AddServiceRecordCommand(
    Guid TenantId,
    Guid VehicleId,
    DateTimeOffset Date,
    string PerformedBy,
    ServiceCategory Category,
    int OdometerKm,
    IReadOnlyList<string> ItemsChanged,
    string Reason,
    IReadOnlyList<ServicePartInput> PartsUsed,
    decimal? LaborHours,
    decimal? CostCad,
    Guid? WorkOrderId,
    string? Notes) : ICommand<Guid>;

/// <summary>A part line on a service record request.</summary>
public sealed record ServicePartInput(string Sku, int Qty);
