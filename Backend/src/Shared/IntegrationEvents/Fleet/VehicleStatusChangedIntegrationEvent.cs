using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Fleet;

/// <summary>
/// Published whenever a Fleet vehicle changes status — routing key
/// <c>fleet.vehicle-status-changed</c>. Statuses travel as strings: integration events
/// are the module's public contract and never reference Fleet's internal enums.
/// <see cref="TenantId"/> is part of the payload because handlers run outside any HTTP
/// request — the consuming module takes its tenant from the event, not from ITenantContext.
/// </summary>
public sealed record VehicleStatusChangedIntegrationEvent(
    Guid VehicleId,
    Guid TenantId,
    string PreviousStatus,
    string NewStatus,
    string? Reason) : IntegrationEvent;
