using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Shared.Kernel;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Domain.Vehicles.Events;

namespace NorthernLink.Fleet.Application.Vehicles;

/// <summary>
/// Fleet's explicit domain-event → integration-event translation. Only status changes
/// are public contract today; everything else stays internal (null). Extending Fleet's
/// public surface means adding a case here plus an event record in
/// NorthernLink.Shared/IntegrationEvents/Fleet/ — never auto-publishing.
/// </summary>
public sealed class FleetIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate) =>
        domainEvent switch
        {
            VehicleStatusChangedDomainEvent statusChanged => new VehicleStatusChangedIntegrationEvent(
                statusChanged.VehicleId,
                ((Vehicle)aggregate).TenantId,
                statusChanged.PreviousStatus.ToString(),
                statusChanged.NewStatus.ToString(),
                statusChanged.Reason),
            _ => null,
        };
}
