using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Assignments.Assign;

/// <summary>
/// Puts a vehicle on a maintenance plan — creating the assignment, or switching an existing
/// one to the new plan (one plan per vehicle).
/// </summary>
public sealed record AssignMaintenancePlanCommand(
    Guid TenantId,
    Guid VehicleId,
    Guid PlanId) : ICommand;
