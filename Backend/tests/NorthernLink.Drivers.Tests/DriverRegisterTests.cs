using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Drivers.Events;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Drivers.Tests;

public class DriverRegisterTests
{
    [Fact]
    public void Valid_driver_is_registered_active_and_raises_the_registered_event()
    {
        var result = TestDrivers.Register();

        Assert.True(result.IsSuccess);
        var driver = result.Value;
        Assert.Equal(TestDrivers.TenantId, driver.TenantId);
        Assert.Equal("J. Spence", driver.Name);
        Assert.Equal("204-555-0147", driver.Phone);
        Assert.Equal("Class 2", driver.LicenceClass);
        Assert.Equal(new DateOnly(2027, 3, 31), driver.LicenceExpiry);
        Assert.Equal("Northern Link", driver.Source);
        Assert.False(driver.HasWorkPermit);
        Assert.Equal(DriverStatus.Active, driver.Status);

        var domainEvent = Assert.Single(driver.DomainEvents);
        var registered = Assert.IsType<DriverRegisteredDomainEvent>(domainEvent);
        Assert.Equal(driver.Id, registered.DriverId);
        Assert.Equal(TestDrivers.TenantId, registered.TenantId);
    }

    [Fact]
    public void Details_are_trimmed_and_blank_phone_becomes_null()
    {
        var result = TestDrivers.Register(name: "  A. Bear  ", phone: "   ", source: " Keewatin Rail Partners ");

        Assert.True(result.IsSuccess);
        Assert.Equal("A. Bear", result.Value.Name);
        Assert.Null(result.Value.Phone);
        Assert.Equal("Keewatin Rail Partners", result.Value.Source);
    }

    [Fact]
    public void Missing_name_is_rejected()
    {
        var result = TestDrivers.Register(name: "  ");

        Assert.True(result.IsFailure);
        Assert.Equal(DriverErrors.NameRequired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Missing_licence_class_is_rejected()
    {
        var result = TestDrivers.Register(licenceClass: "");

        Assert.True(result.IsFailure);
        Assert.Equal(DriverErrors.LicenceClassRequired, result.Error);
    }

    [Fact]
    public void Missing_source_is_rejected()
    {
        var result = TestDrivers.Register(source: " ");

        Assert.True(result.IsFailure);
        Assert.Equal(DriverErrors.SourceRequired, result.Error);
    }
}
