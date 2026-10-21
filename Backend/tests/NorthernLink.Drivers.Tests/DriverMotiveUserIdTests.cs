using NorthernLink.Drivers.Application.Drivers;
using NorthernLink.Drivers.Application.Drivers.SetMotiveUser;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Drivers.Events;
using NorthernLink.Drivers.Tests.Hos;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Drivers.Tests;

/// <summary>
/// The driver's Motive (ELD) user id — the key the Motive HOS sync will match on. Set, clear,
/// validation, the no-op on an unchanged value, per-tenant uniqueness in the handler, and the
/// response carrying it.
/// </summary>
public class DriverMotiveUserIdTests
{
    private static Driver NewDriver(string name = "J. Spence")
    {
        var result = TestDrivers.Register(name: name);
        Assert.True(result.IsSuccess);
        result.Value.ClearDomainEvents();
        return result.Value;
    }

    // ---- Domain ----

    [Fact]
    public void A_newly_registered_driver_has_no_motive_user() =>
        Assert.Null(NewDriver().MotiveUserId);

    [Fact]
    public void Setting_trims_and_raises_its_own_event()
    {
        var driver = NewDriver();

        var result = driver.SetMotiveUserId("  90210 ");

        Assert.True(result.IsSuccess);
        Assert.Equal("90210", driver.MotiveUserId);
        var raised = Assert.IsType<DriverMotiveUserIdChangedDomainEvent>(Assert.Single(driver.DomainEvents));
        Assert.Equal(driver.Id, raised.DriverId);
        Assert.Null(raised.PreviousMotiveUserId);
        Assert.Equal("90210", raised.MotiveUserId);
    }

    [Fact]
    public void Setting_the_same_value_again_is_a_no_op_without_an_event()
    {
        var driver = NewDriver();
        driver.SetMotiveUserId("90210");
        driver.ClearDomainEvents();

        Assert.True(driver.SetMotiveUserId("90210").IsSuccess);
        Assert.Empty(driver.DomainEvents);
    }

    [Fact]
    public void Clearing_removes_the_mapping()
    {
        var driver = NewDriver();
        driver.SetMotiveUserId("90210");
        driver.ClearDomainEvents();

        Assert.True(driver.SetMotiveUserId(null).IsSuccess);
        Assert.Null(driver.MotiveUserId);
        var raised = Assert.IsType<DriverMotiveUserIdChangedDomainEvent>(Assert.Single(driver.DomainEvents));
        Assert.Equal("90210", raised.PreviousMotiveUserId);
        Assert.Null(raised.MotiveUserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_value_is_refused_rather_than_read_as_clear(string value) =>
        Assert.Equal(DriverErrors.MotiveUserIdRequired, NewDriver().SetMotiveUserId(value).Error);

    [Fact]
    public void The_length_cap_counts_the_trimmed_value()
    {
        var atCap = new string('9', Driver.MotiveUserIdMaxLength);

        Assert.True(NewDriver().SetMotiveUserId($" {atCap} ").IsSuccess);
        Assert.Equal(DriverErrors.MotiveUserIdTooLong, NewDriver().SetMotiveUserId(atCap + "9").Error);
    }

    // ---- Handler ----

    [Fact]
    public async Task Unknown_driver_is_not_found()
    {
        var handler = new SetDriverMotiveUserIdCommandHandler(new HosTestSupport.FakeDriverRepository());

        var result = await handler.Handle(
            new SetDriverMotiveUserIdCommand(TestDrivers.TenantId, Guid.NewGuid(), "90210"), CancellationToken.None);

        Assert.Equal(DriverErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Handler_sets_and_saves()
    {
        var driver = NewDriver();
        var repository = new HosTestSupport.FakeDriverRepository(driver);

        var result = await new SetDriverMotiveUserIdCommandHandler(repository).Handle(
            new SetDriverMotiveUserIdCommand(TestDrivers.TenantId, driver.Id, "90210"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("90210", driver.MotiveUserId);
        Assert.Equal(1, repository.Saves);
    }

    [Fact]
    public async Task A_motive_user_already_on_another_driver_is_a_conflict_and_nothing_saves()
    {
        var first = NewDriver("A. One");
        first.SetMotiveUserId("90210");
        var second = NewDriver("B. Two");
        var repository = new HosTestSupport.FakeDriverRepository(first, second);

        var result = await new SetDriverMotiveUserIdCommandHandler(repository).Handle(
            new SetDriverMotiveUserIdCommand(TestDrivers.TenantId, second.Id, " 90210 "), CancellationToken.None);

        Assert.Equal(DriverErrors.DuplicateMotiveUserId, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(0, repository.Saves);
    }

    [Fact]
    public async Task Re_setting_its_own_value_is_not_a_conflict()
    {
        var driver = NewDriver();
        driver.SetMotiveUserId("90210");
        var repository = new HosTestSupport.FakeDriverRepository(driver);

        var result = await new SetDriverMotiveUserIdCommandHandler(repository).Handle(
            new SetDriverMotiveUserIdCommand(TestDrivers.TenantId, driver.Id, "90210"), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    // ---- Contract ----

    [Fact]
    public void The_response_carries_the_motive_user_id()
    {
        var driver = NewDriver();
        driver.SetMotiveUserId("90210");

        var response = DriverResponseMapper.ToResponse(driver, 0, null, null, null, null);

        Assert.Equal("90210", response.MotiveUserId);
    }

    [Fact]
    public void The_mapping_event_stays_internal_to_drivers()
    {
        var driver = NewDriver();
        driver.SetMotiveUserId("90210");

        Assert.Null(new DriversIntegrationEventMapper().Map(Assert.Single(driver.DomainEvents), driver));
    }
}
