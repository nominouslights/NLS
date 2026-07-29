using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Inspections.Remove;

/// <summary>
/// Hard-deletes an inspection. REMOVE is a permanent delete (product decision); the pre-delete
/// state still lands in the audit snapshot/journal, and a post-trip removal re-gates the trip's
/// completion via the <c>fleet.vehicle-inspection-removed</c> integration event.
/// </summary>
public sealed record RemoveInspectionCommand(Guid InspectionId) : ICommand;
