using NorthernLink.Drivers.Application.Drivers;
using NorthernLink.Drivers.Domain.Clearances;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Drivers.Events;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Drivers.Domain.Hos.Events;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.Events;
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

    // ---- DutyDay → drivers.duty-day-changed ----

    [Fact]
    public void An_appended_duty_event_maps_to_duty_day_changed_with_the_effective_timeline()
    {
        var day = Hos.HosTestSupport.DayWith(
            Hos.HosTestSupport.Draft("06:00", DutyStatus.OnDuty),
            Hos.HosTestSupport.Draft("06:30", DutyStatus.Driving));
        Assert.True(day.Append(Hos.HosTestSupport.Draft("12:00", DutyStatus.OffDuty), Hos.HosTestSupport.Now).IsSuccess);
        var domainEvent = Assert.IsType<DutyEventAppendedDomainEvent>(Assert.Single(day.DomainEvents));

        var result = _mapper.Map(domainEvent, day);

        var integrationEvent = Assert.IsType<DutyDayChangedIntegrationEvent>(result);
        Assert.Equal(day.Id, integrationEvent.DutyDayId);
        Assert.Equal(TestDrivers.TenantId, integrationEvent.TenantId);
        Assert.Equal(Hos.HosTestSupport.DriverId, integrationEvent.DriverId);
        Assert.Equal(Hos.HosTestSupport.D, integrationEvent.LocalDate);
        Assert.False(integrationEvent.Certified);
        Assert.Equal(0m, integrationEvent.PersonalConveyanceKm);
        Assert.Equal(day.Version, integrationEvent.Version);
        Assert.Equal(["On Duty", "Driving", "Off Duty"], integrationEvent.EffectiveEvents.Select(e => e.Status));
        Assert.All(integrationEvent.EffectiveEvents, e => Assert.Equal("Driver App", e.Source));
        Assert.Equal(Hos.HosTestSupport.At("06:30"), integrationEvent.EffectiveEvents[1].AtUtc);
        Assert.Equal("drivers.duty-day-changed", EventRoutingKey.For(typeof(DutyDayChangedIntegrationEvent)));
    }

    [Fact]
    public void An_amendment_maps_without_the_voided_event_and_with_the_odometers()
    {
        var closer = Guid.NewGuid();
        var day = Hos.HosTestSupport.DayWith(
            Hos.HosTestSupport.Draft("06:00", DutyStatus.OffDuty),
            Hos.HosTestSupport.Draft("10:00", DutyStatus.PersonalConveyance, startKm: 1000m, attest: true),
            Hos.HosTestSupport.Draft("11:00", DutyStatus.OffDuty, endKm: 1030m, eventId: closer));
        Assert.True(day.Amend(closer, Hos.HosTestSupport.Draft("11:00", DutyStatus.OffDuty, endKm: 1040m), "misread", Hos.HosTestSupport.Now).IsSuccess);
        var domainEvent = Assert.IsType<DutyEventAmendedDomainEvent>(Assert.Single(day.DomainEvents));

        var integrationEvent = Assert.IsType<DutyDayChangedIntegrationEvent>(_mapper.Map(domainEvent, day));

        Assert.Equal(3, integrationEvent.EffectiveEvents.Count);
        Assert.DoesNotContain(integrationEvent.EffectiveEvents, e => e.EventId == closer);
        Assert.Equal(1000m, integrationEvent.EffectiveEvents[1].OdometerStartKm);
        Assert.Equal(1040m, integrationEvent.EffectiveEvents[2].OdometerEndKm);
        Assert.Equal(40m, integrationEvent.PersonalConveyanceKm);
    }

    [Fact]
    public void Certification_maps_to_duty_day_changed_with_certified_true()
    {
        var day = Hos.HosTestSupport.DayWith(
            Hos.HosTestSupport.Draft("06:00", DutyStatus.OnDuty),
            Hos.HosTestSupport.Draft("14:00", DutyStatus.OffDuty));
        Assert.True(day.Certify(Hos.HosTestSupport.UserId, Hos.HosTestSupport.Now).IsSuccess);
        var domainEvent = Assert.IsType<DutyDayCertifiedDomainEvent>(Assert.Single(day.DomainEvents));

        var integrationEvent = Assert.IsType<DutyDayChangedIntegrationEvent>(_mapper.Map(domainEvent, day));

        Assert.True(integrationEvent.Certified);
    }

    [Fact]
    public void The_duty_day_event_serialises_on_the_shared_wire_format()
    {
        var day = Hos.HosTestSupport.DayWith(Hos.HosTestSupport.Draft("06:00", DutyStatus.OnDuty));
        Assert.True(day.Append(Hos.HosTestSupport.Draft("07:00", DutyStatus.Driving), Hos.HosTestSupport.Now).IsSuccess);
        var integrationEvent = _mapper.Map(day.DomainEvents.Single(), day)!;

        var json = IntegrationEventJson.Serialize(integrationEvent);

        Assert.Contains("\"effectiveEvents\":[", json);
        Assert.Contains("\"status\":\"On Duty\"", json);
        Assert.Contains("\"localDate\":\"2026-09-14\"", json);
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
