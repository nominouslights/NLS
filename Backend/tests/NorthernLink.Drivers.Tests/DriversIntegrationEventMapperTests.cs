using NorthernLink.Drivers.Application.Drivers;
using NorthernLink.Drivers.Domain.Clearances;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Drivers.Events;
using NorthernLink.Shared.IntegrationEvents.Drivers;
using Xunit;

namespace NorthernLink.Drivers.Tests;

public class DriversIntegrationEventMapperTests
{
    private readonly DriversIntegrationEventMapper _mapper = new();

    [Fact]
    public void Registration_maps_to_the_driver_changed_event_with_current_state()
    {
        var driver = TestDrivers.Register().Value;
        var domainEvent = Assert.Single(driver.DomainEvents);

        var result = _mapper.Map(domainEvent, driver);

        var integrationEvent = Assert.IsType<DriverChangedIntegrationEvent>(result);
        Assert.Equal(driver.Id, integrationEvent.DriverId);
        Assert.Equal(TestDrivers.TenantId, integrationEvent.TenantId);
        Assert.Equal("J. Spence", integrationEvent.Name);
        Assert.Equal("Class 2", integrationEvent.LicenceClass);
        Assert.Equal("Active", integrationEvent.Status);
        Assert.Equal("Northern Link", integrationEvent.Source);
    }

    [Fact]
    public void Update_maps_to_the_driver_changed_event_with_the_new_details()
    {
        var driver = TestDrivers.Register().Value;
        driver.ClearDomainEvents();
        Assert.True(driver.Update("J. Spence Jr.", null, "Class 4", null, "Keewatin Rail Partners", true).IsSuccess);
        var domainEvent = Assert.IsType<DriverUpdatedDomainEvent>(Assert.Single(driver.DomainEvents));

        var result = _mapper.Map(domainEvent, driver);

        var integrationEvent = Assert.IsType<DriverChangedIntegrationEvent>(result);
        Assert.Equal(driver.Id, integrationEvent.DriverId);
        Assert.Equal("J. Spence Jr.", integrationEvent.Name);
        Assert.Equal("Class 4", integrationEvent.LicenceClass);
        Assert.Equal("Keewatin Rail Partners", integrationEvent.Source);
    }

    [Fact]
    public void Status_change_maps_to_the_driver_changed_event_with_the_new_status()
    {
        var driver = TestDrivers.Register().Value;
        driver.ClearDomainEvents();
        Assert.True(driver.ChangeStatus(DriverStatus.Inactive).IsSuccess);
        var domainEvent = Assert.IsType<DriverStatusChangedDomainEvent>(Assert.Single(driver.DomainEvents));

        var result = _mapper.Map(domainEvent, driver);

        var integrationEvent = Assert.IsType<DriverChangedIntegrationEvent>(result);
        Assert.Equal(driver.Id, integrationEvent.DriverId);
        Assert.Equal("Inactive", integrationEvent.Status);
    }

    [Fact]
    public void Unrelated_domain_events_stay_internal()
    {
        var clearance = DriverClearance.Grant(
            TestDrivers.TenantId, Guid.NewGuid(), "Site Induction", "Alamos Gold", null).Value;

        var result = _mapper.Map(new UnrelatedDomainEvent(), clearance);

        Assert.Null(result);
    }

    private sealed record UnrelatedDomainEvent : NorthernLink.Shared.Kernel.IDomainEvent
    {
        public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    }
}
