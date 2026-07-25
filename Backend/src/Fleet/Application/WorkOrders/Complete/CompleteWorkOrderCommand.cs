using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Services.Add;
using NorthernLink.Fleet.Domain.Services;

namespace NorthernLink.Fleet.Application.WorkOrders.Complete;

/// <summary>
/// Completes a work order by logging the service record that resolved it (WHO/WHAT/WHY),
/// then closing the work order — one transaction. Returns the new service record's id.
/// </summary>
public sealed record CompleteWorkOrderCommand(
    Guid TenantId,
    Guid WorkOrderId,
    DateTimeOffset Date,
    string PerformedBy,
    ServiceCategory Category,
    int OdometerKm,
    IReadOnlyList<string> ItemsChanged,
    string Reason,
    IReadOnlyList<ServicePartInput> PartsUsed,
    decimal? LaborHours,
    decimal? CostCad,
    string? Notes) : ICommand<Guid>;
