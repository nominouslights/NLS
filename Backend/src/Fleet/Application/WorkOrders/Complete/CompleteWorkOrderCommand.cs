using NorthernLink.Shared.Messaging;
using NorthernLink.Fleet.Application.Services.Add;
using NorthernLink.Fleet.Domain.Services;
using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Application.WorkOrders.Complete;

/// <summary>
/// Completes a work order by logging the service record that resolved it (WHO/WHAT/WHY),
/// then closing the work order — one transaction. Returns the new service record's id.
///
/// <paramref name="DefectOutcomes"/> is required — one per defect line — when the work order
/// carries defect lines, and must be empty when it does not.
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
    string? Notes,
    IReadOnlyList<WorkOrderDefectOutcome>? DefectOutcomes = null) : ICommand<Guid>;
