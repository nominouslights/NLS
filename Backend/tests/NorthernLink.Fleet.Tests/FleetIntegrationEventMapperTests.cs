using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Domain.Vehicles.Events;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class FleetIntegrationEventMapperTests
{
    private readonly FleetIntegrationEventMapper _mapper = new();

    [Fact]
    public void Status_change_maps_to_public_integration_event()
    {
        var vehicle = TestVehicles.Create();
        var domainEvent = new VehicleStatusChangedDomainEvent(
            vehicle.Id, VehicleStatus.Active, VehicleStatus.InMaintenance, "Brake inspection");

        var result = _mapper.Map(domainEvent, vehicle);

        var integrationEvent = Assert.IsType<VehicleStatusChangedIntegrationEvent>(result);
        Assert.Equal(vehicle.Id, integrationEvent.VehicleId);
        Assert.Equal(vehicle.TenantId, integrationEvent.TenantId);
        Assert.Equal("Active", integrationEvent.PreviousStatus);
        Assert.Equal("InMaintenance", integrationEvent.NewStatus);
        Assert.Equal("Brake inspection", integrationEvent.Reason);
    }

    [Fact]
    public void Other_domain_events_stay_internal()
    {
        var vehicle = TestVehicles.Create();

        Assert.Null(_mapper.Map(new VehicleRegisteredDomainEvent(vehicle.Id, vehicle.TenantId), vehicle));
        Assert.Null(_mapper.Map(
            new VehicleReachedEndOfLifeDomainEvent(vehicle.Id, OdometerKm: 500_000, EndOfLifeKm: 500_000), vehicle));
    }
}
