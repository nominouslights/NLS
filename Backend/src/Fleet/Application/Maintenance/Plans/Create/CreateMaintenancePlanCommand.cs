using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Plans.Create;

/// <summary>
/// Creates a maintenance plan (name unique per tenant) with its routine items and
/// overhauls. Returns the new plan's id.
/// </summary>
public sealed record CreateMaintenancePlanCommand(
    Guid TenantId,
    string Name,
    string VehicleModel,
    string ServiceClass,
    string? Notes,
    IReadOnlyList<MaintenanceItem> Items,
    IReadOnlyList<OverhaulSpec> Overhauls) : ICommand<Guid>;
