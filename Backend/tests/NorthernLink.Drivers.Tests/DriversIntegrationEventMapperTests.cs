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
        Assert.Null(integrationEvent.UserId); // nobody can sign in as a freshly registered driver
    }

    [Fact]
    public void Linking_a_user_maps_to_the_driver_changed_event_carrying_the_user_id()
    {
        // Trips resolves a Driver Field App caller to "their" driver through this field: an
        // unmapped link would leave the account refused on every trip route.
        var userId = Guid.NewGuid();
        var driver = TestDrivers.Register().Value;
        driver.ClearDomainEvents();
        Assert.True(driver.LinkUser(userId).IsSuccess);
        var domainEvent = Assert.IsType<DriverUserLinkedDomainEvent>(Assert.Single(driver.DomainEvents));

        var result = _mapper.Map(domainEvent, driver);

        var integrationEvent = Assert.IsType<DriverChangedIntegrationEvent>(result);
        Assert.Equal(driver.Id, integrationEvent.DriverId);
        Assert.Equal(userId, integrationEvent.UserId);
        Assert.Equal("Active", integrationEvent.Status);
    }

    [Fact]
    public void Unlinking_maps_to_the_driver_changed_event_with_a_null_user_id()
    {
        // The unlink revokes access; the replica must see the null, not keep the old id.
        var driver = TestDrivers.Register().Value;
        Assert.True(driver.LinkUser(Guid.NewGuid()).IsSuccess);
        driver.ClearDomainEvents();
        Assert.True(driver.UnlinkUser().IsSuccess);
        var domainEvent = Assert.IsType<DriverUserUnlinkedDomainEvent>(Assert.Single(driver.DomainEvents));

        var result = _mapper.Map(domainEvent, driver);

        var integrationEvent = Assert.IsType<DriverChangedIntegrationEvent>(result);
        Assert.Null(integrationEvent.UserId);
    }

    [Fact]
    public void Every_mapping_carries_the_current_user_id()
    {
        // An update or status change after a link must not replay the row without the link.
        var userId = Guid.NewGuid();
        var driver = TestDrivers.Register().Value;
        Assert.True(driver.LinkUser(userId).IsSuccess);
        driver.ClearDomainEvents();
        Assert.True(driver.ChangeStatus(DriverStatus.Inactive).IsSuccess);
        var domainEvent = Assert.Single(driver.DomainEvents);

        var integrationEvent = Assert.IsType<DriverChangedIntegrationEvent>(_mapper.Map(domainEvent, driver));

        Assert.Equal(userId, integrationEvent.UserId);
        Assert.Equal("Inactive", integrationEvent.Status);
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
