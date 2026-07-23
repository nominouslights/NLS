using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Drivers.Events;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Drivers.Tests;

public class DriverUpdateAndStatusTests
{
    [Fact]
    public void Update_changes_details_and_raises_the_updated_event()
    {
        var driver = TestDrivers.Register().Value;
        driver.ClearDomainEvents();

        var result = driver.Update(
            "J. Spence Jr.",
            null,
            "Class 4",
            new DateOnly(2028, 6, 30),
            "Northern Link",
            hasWorkPermit: true);

        Assert.True(result.IsSuccess);
        Assert.Equal("J. Spence Jr.", driver.Name);
        Assert.Null(driver.Phone);
        Assert.Equal("Class 4", driver.LicenceClass);
        Assert.Equal(new DateOnly(2028, 6, 30), driver.LicenceExpiry);
        Assert.True(driver.HasWorkPermit);

        var domainEvent = Assert.Single(driver.DomainEvents);
        var updated = Assert.IsType<DriverUpdatedDomainEvent>(domainEvent);
        Assert.Equal(driver.Id, updated.DriverId);
    }

    [Fact]
    public void Update_with_missing_name_is_rejected_and_changes_nothing()
    {
        var driver = TestDrivers.Register().Value;
        driver.ClearDomainEvents();

        var result = driver.Update(" ", null, "Class 4", null, "Northern Link", false);

        Assert.True(result.IsFailure);
        Assert.Equal(DriverErrors.NameRequired, result.Error);
        Assert.Equal("J. Spence", driver.Name);
        Assert.Empty(driver.DomainEvents);
    }

    [Theory]
    [InlineData(DriverStatus.Active, DriverStatus.Inactive, true)]
    [InlineData(DriverStatus.Active, DriverStatus.Deactivated, true)]
    [InlineData(DriverStatus.Inactive, DriverStatus.Active, true)]
    [InlineData(DriverStatus.Inactive, DriverStatus.Deactivated, true)]
    [InlineData(DriverStatus.Deactivated, DriverStatus.Active, true)]
    [InlineData(DriverStatus.Deactivated, DriverStatus.Inactive, false)]
    [InlineData(DriverStatus.Active, DriverStatus.Active, false)]
    [InlineData(DriverStatus.Inactive, DriverStatus.Inactive, false)]
    [InlineData(DriverStatus.Deactivated, DriverStatus.Deactivated, false)]
    public void The_transition_matrix_is_enforced(DriverStatus from, DriverStatus to, bool allowed)
    {
        Assert.Equal(allowed, Driver.CanTransition(from, to));
    }

    [Fact]
    public void A_valid_transition_changes_status_and_raises_the_event()
    {
        var driver = TestDrivers.Register().Value;
        driver.ClearDomainEvents();

        var result = driver.ChangeStatus(DriverStatus.Inactive);

        Assert.True(result.IsSuccess);
        Assert.Equal(DriverStatus.Inactive, driver.Status);

        var domainEvent = Assert.Single(driver.DomainEvents);
        var changed = Assert.IsType<DriverStatusChangedDomainEvent>(domainEvent);
        Assert.Equal(driver.Id, changed.DriverId);
        Assert.Equal(DriverStatus.Active, changed.PreviousStatus);
        Assert.Equal(DriverStatus.Inactive, changed.NewStatus);
    }

    [Fact]
    public void A_deactivated_driver_cannot_move_to_inactive()
    {
        var driver = TestDrivers.Register().Value;
        Assert.True(driver.ChangeStatus(DriverStatus.Deactivated).IsSuccess);
        driver.ClearDomainEvents();

        var result = driver.ChangeStatus(DriverStatus.Inactive);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Drivers.Driver.InvalidStatusTransition", result.Error.Code);
        Assert.Equal(DriverStatus.Deactivated, driver.Status);
        Assert.Empty(driver.DomainEvents);
    }

    [Fact]
    public void Same_status_is_not_a_transition()
    {
        var driver = TestDrivers.Register().Value;
        driver.ClearDomainEvents();

        var result = driver.ChangeStatus(DriverStatus.Active);

        Assert.True(result.IsFailure);
        Assert.Equal("Drivers.Driver.InvalidStatusTransition", result.Error.Code);
        Assert.Empty(driver.DomainEvents);
    }

    [Fact]
    public void A_deactivated_driver_can_be_reinstated_to_active()
    {
        var driver = TestDrivers.Register().Value;
        Assert.True(driver.ChangeStatus(DriverStatus.Deactivated).IsSuccess);

        var result = driver.ChangeStatus(DriverStatus.Active);

        Assert.True(result.IsSuccess);
        Assert.Equal(DriverStatus.Active, driver.Status);
    }
}
