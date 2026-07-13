using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Fleet;

namespace NorthernLink.Trips.Application.Integration;

/// <summary>
/// Trips' reaction to Fleet vehicle status changes — the platform's first cross-module
/// consumer, and the template every future one copies. Log-only until the Trips domain
/// exists; then it injects ISender and dispatches a real command (e.g. reassigning
/// active trips off a vehicle that went out of service), taking the tenant from the
/// event payload — handlers run outside any HTTP request, so ITenantContext is empty here.
/// Delivery is at-least-once: keep this idempotent keyed on the event id.
/// </summary>
public sealed class VehicleStatusChangedIntegrationEventHandler(
    ILogger<VehicleStatusChangedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<VehicleStatusChangedIntegrationEvent>
{
    public Task Handle(VehicleStatusChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Trips received vehicle status change ({EventId}): vehicle {VehicleId} of tenant {TenantId} went {PreviousStatus} -> {NewStatus} ({Reason})",
            integrationEvent.EventId,
            integrationEvent.VehicleId,
            integrationEvent.TenantId,
            integrationEvent.PreviousStatus,
            integrationEvent.NewStatus,
            integrationEvent.Reason ?? "no reason given");

        return Task.CompletedTask;
    }
}
