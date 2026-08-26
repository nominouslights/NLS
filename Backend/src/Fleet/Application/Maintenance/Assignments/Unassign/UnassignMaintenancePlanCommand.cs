using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Assignments.Unassign;

/// <summary>
/// Takes a vehicle off its maintenance plan (hard-deletes the assignment). Completion
/// history is untouched — reassigning later resumes due math from the same records.
/// </summary>
public sealed record UnassignMaintenancePlanCommand(
    Guid TenantId,
    Guid VehicleId) : ICommand;
