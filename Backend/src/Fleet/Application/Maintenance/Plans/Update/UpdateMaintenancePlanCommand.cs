using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Plans.Update;

/// <summary>
/// Replaces a plan's details, items, and overhauls wholesale (codes are the identity, so
/// there is no per-line merge). Edits are retroactive by design — due status is computed
/// from the current intervals, never stored.
/// </summary>
public sealed record UpdateMaintenancePlanCommand(
    Guid TenantId,
    Guid PlanId,
    string Name,
    string VehicleModel,
    string ServiceClass,
    string? Notes,
    IReadOnlyList<MaintenanceItem> Items,
    IReadOnlyList<OverhaulSpec> Overhauls) : ICommand;
