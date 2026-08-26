using NorthernLink.Trips.Application.Riders.SetRotation;
using NorthernLink.Trips.Domain.Riders;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class SetRiderRotationCommandHandlerTests
{
    private readonly FakeRiderRepository _riders = new();

    private SetRiderRotationCommandHandler Handler => new(_riders);

    [Fact]
    public async Task An_unknown_rider_is_not_found()
    {
        var result = await Handler.Handle(
            new SetRiderRotationCommand(Guid.NewGuid(), 20), CancellationToken.None);

        Assert.Equal(RiderErrors.NotFound, result.Error);
        Assert.Equal(0, _riders.SaveCount);
    }

    [Fact]
    public async Task A_valid_rotation_is_stored()
    {
        var rider = TestRiders.Create().Value;
        _riders.Add(rider);

        var result = await Handler.Handle(
            new SetRiderRotationCommand(rider.Id, 20), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(20, rider.RotationDays);
        Assert.Equal(1, _riders.SaveCount);
    }

    [Fact]
    public async Task A_domain_rejection_propagates_without_saving()
    {
        var rider = TestRiders.Create().Value;
        _riders.Add(rider);

        var result = await Handler.Handle(
            new SetRiderRotationCommand(rider.Id, 7), CancellationToken.None);

        Assert.Equal(RiderErrors.InvalidRotation, result.Error);
        Assert.Equal(0, _riders.SaveCount);
    }

    [Fact]
    public async Task Null_clears_the_rotation()
    {
        var rider = TestRiders.Create().Value;
        Assert.True(rider.SetRotation(10).IsSuccess);
        _riders.Add(rider);

        var result = await Handler.Handle(
            new SetRiderRotationCommand(rider.Id, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(rider.RotationDays);
    }
}
