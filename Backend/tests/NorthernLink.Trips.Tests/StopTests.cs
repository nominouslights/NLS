using NorthernLink.Trips.Domain.Stops;
using NorthernLink.Trips.Domain.Stops.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class StopTests
{
    private static Address ThompsonAddress() =>
        Address.Create("123 Mystery Lake Rd", "Thompson", "Manitoba", "R8N 0M4", "Canada").Value;

    private static Coordinate ThompsonCoordinate() => Coordinate.Create(55.74, -97.85).Value;

    [Fact]
    public void Create_returns_an_active_stop_with_fields_set_and_raises_created_event()
    {
        var result = Stop.Create(
            TestPlanning.TenantId,
            "  Thompson Hub  ",
            StopType.Hub,
            ThompsonAddress(),
            ThompsonCoordinate(),
            "  Main dispatch yard  ");

        Assert.True(result.IsSuccess);
        var stop = result.Value;
        Assert.Equal(TestPlanning.TenantId, stop.TenantId);
        Assert.Equal("Thompson Hub", stop.Name);
        Assert.Equal(StopType.Hub, stop.Type);
        Assert.Equal("Main dispatch yard", stop.Notes);
        Assert.True(stop.Active);
        Assert.NotEqual(default, stop.CreatedAtUtc);
        Assert.Equal(stop.CreatedAtUtc, stop.UpdatedAtUtc);
        Assert.Single(stop.DomainEvents);
        Assert.IsType<StopCreatedDomainEvent>(Assert.Single(stop.DomainEvents));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_name(string? name)
    {
        var result = Stop.Create(
            TestPlanning.TenantId, name!, StopType.Hub, ThompsonAddress(), ThompsonCoordinate(), null);

        Assert.True(result.IsFailure);
        Assert.Equal(StopErrors.NameRequired, result.Error);
    }

    [Fact]
    public void Update_mutates_fields_and_raises_updated_event()
    {
        var stop = Stop.Create(
            TestPlanning.TenantId, "Thompson Hub", StopType.Hub, ThompsonAddress(), ThompsonCoordinate(), null).Value;
        stop.ClearDomainEvents();

        var newAddress = Address.Create(null, "Lynn Lake", "Manitoba", null, "Canada").Value;
        var newCoordinate = Coordinate.Create(56.85, -101.05).Value;

        var result = stop.Update(
            "Lynn Lake Depot", StopType.Community, newAddress, newCoordinate, "Renamed", active: true);

        Assert.True(result.IsSuccess);
        Assert.Equal("Lynn Lake Depot", stop.Name);
        Assert.Equal(StopType.Community, stop.Type);
        Assert.Same(newAddress, stop.Address);
        Assert.Same(newCoordinate, stop.Coordinate);
        Assert.Equal("Renamed", stop.Notes);
        Assert.True(stop.Active);
        Assert.IsType<StopUpdatedDomainEvent>(Assert.Single(stop.DomainEvents));
    }

    [Fact]
    public void Update_rejects_a_blank_name()
    {
        var stop = Stop.Create(
            TestPlanning.TenantId, "Thompson Hub", StopType.Hub, ThompsonAddress(), ThompsonCoordinate(), null).Value;

        var result = stop.Update(
            "  ", StopType.Hub, ThompsonAddress(), ThompsonCoordinate(), null, active: true);

        Assert.True(result.IsFailure);
        Assert.Equal(StopErrors.NameRequired, result.Error);
        Assert.Equal("Thompson Hub", stop.Name);
    }

    [Fact]
    public void Update_can_deactivate_via_active_false()
    {
        var stop = Stop.Create(
            TestPlanning.TenantId, "Thompson Hub", StopType.Hub, ThompsonAddress(), ThompsonCoordinate(), null).Value;

        var result = stop.Update(
            "Thompson Hub", StopType.Hub, ThompsonAddress(), ThompsonCoordinate(), null, active: false);

        Assert.True(result.IsSuccess);
        Assert.False(stop.Active);
    }

    [Fact]
    public void SetActive_toggles_and_raises_updated_event_on_change()
    {
        var stop = Stop.Create(
            TestPlanning.TenantId, "Thompson Hub", StopType.Hub, ThompsonAddress(), ThompsonCoordinate(), null).Value;
        stop.ClearDomainEvents();

        stop.SetActive(false);

        Assert.False(stop.Active);
        Assert.IsType<StopUpdatedDomainEvent>(Assert.Single(stop.DomainEvents));

        stop.SetActive(true);
        Assert.True(stop.Active);
    }

    [Fact]
    public void SetActive_is_a_no_op_when_the_flag_is_unchanged()
    {
        var stop = Stop.Create(
            TestPlanning.TenantId, "Thompson Hub", StopType.Hub, ThompsonAddress(), ThompsonCoordinate(), null).Value;
        stop.ClearDomainEvents();

        stop.SetActive(true);

        Assert.True(stop.Active);
        Assert.Empty(stop.DomainEvents);
    }

    /// <summary>
    /// Terminus is the classification the NL-TRM-01 terminus summary selects venues by, so it has
    /// to survive both the create and the update path — a stop is routinely promoted to Terminus
    /// long after it was first catalogued as an ordinary Hub or PickupPoint.
    /// </summary>
    [Fact]
    public void A_stop_can_be_created_as_a_Terminus_and_promoted_to_one_later()
    {
        var terminus = Stop.Create(
            TestPlanning.TenantId,
            "Best Western Hotel & Suites",
            StopType.Terminus,
            ThompsonAddress(),
            ThompsonCoordinate(),
            null).Value;

        Assert.Equal(StopType.Terminus, terminus.Type);

        var promoted = Stop.Create(
            TestPlanning.TenantId, "Lynn Inn", StopType.PickupPoint, ThompsonAddress(), ThompsonCoordinate(), null)
            .Value;

        var result = promoted.Update(
            "Lynn Inn", StopType.Terminus, ThompsonAddress(), ThompsonCoordinate(), null, active: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(StopType.Terminus, promoted.Type);
    }
}
