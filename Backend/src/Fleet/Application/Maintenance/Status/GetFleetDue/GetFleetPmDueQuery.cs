using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Status.GetFleetDue;

/// <summary>
/// The fleet-wide PM dashboard view — every assigned, non-disposed vehicle with its
/// DueSoon/Overdue counts and due entries.
/// </summary>
public sealed record GetFleetPmDueQuery(Guid TenantId)
    : IQuery<FleetPmDueResponse>;
