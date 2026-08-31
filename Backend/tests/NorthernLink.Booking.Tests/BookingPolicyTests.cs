using NorthernLink.Booking.Application.Settings.GetSettings;
using NorthernLink.Booking.Application.Settings.UpdatePolicy;
using NorthernLink.Booking.Domain.Settings;
using Xunit;

namespace NorthernLink.Booking.Tests;

/// <summary>
/// BookingPolicy get-or-create semantics: reads never 404 (built-in defaults until a row
/// exists), and the first policy write materializes the row.
/// </summary>
public class BookingPolicyTests
{
    [Fact]
    public void CreateDefault_carries_the_documented_defaults()
    {
        var policy = BookingPolicy.CreateDefault(TestBookings.TenantId);

        Assert.Equal(12, policy.CancellationWindowHours);
        Assert.Equal(0m, policy.EarlyCancellationPenaltyCad);
        Assert.Equal(2, policy.BookingCutoffHours);
        Assert.Equal(30, policy.SeatHoldMinutes);
        Assert.Equal(3, policy.DefaultPassengerMinimum);
        Assert.Equal(7, policy.DefaultSeatCapacity);
    }

    [Fact]
    public async Task GetSettings_returns_defaults_when_no_policy_row_exists()
    {
        var handler = new GetBookingSettingsQueryHandler(
            new InMemoryBookingPolicyRepository(),
            new InMemoryCorridorSettingsRepository(),
            new InMemoryCorridorLookupRepository());

        var result = await handler.Handle(new GetBookingSettingsQuery(TestBookings.TenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var policy = result.Value.Policy;
        Assert.False(policy.IsPersisted);
        Assert.Equal(BookingPolicy.DefaultCancellationWindowHours, policy.CancellationWindowHours);
        Assert.Equal(BookingPolicy.DefaultSeatHoldMinutes, policy.SeatHoldMinutes);
        Assert.Equal(BookingPolicy.DefaultPassengerMinimumValue, policy.DefaultPassengerMinimum);
        Assert.Equal(BookingPolicy.DefaultSeatCapacityValue, policy.DefaultSeatCapacity);
        Assert.Empty(result.Value.Corridors);
    }

    [Fact]
    public async Task UpdatePolicy_creates_the_row_when_absent()
    {
        var repository = new InMemoryBookingPolicyRepository();
        var handler = new UpdateBookingPolicyCommandHandler(repository);

        var result = await handler.Handle(
            new UpdateBookingPolicyCommand(TestBookings.TenantId, 24, 25m, 4, 45, 5, 14),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(repository.Policy);
        Assert.Equal(TestBookings.TenantId, repository.Policy!.TenantId);
        Assert.Equal(24, repository.Policy.CancellationWindowHours);
        Assert.Equal(25m, repository.Policy.EarlyCancellationPenaltyCad);
        Assert.Equal(45, repository.Policy.SeatHoldMinutes);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdatePolicy_edits_the_existing_row()
    {
        var repository = new InMemoryBookingPolicyRepository
        {
            Policy = BookingPolicy.CreateDefault(TestBookings.TenantId),
        };
        var existingId = repository.Policy.Id;
        var handler = new UpdateBookingPolicyCommandHandler(repository);

        var result = await handler.Handle(
            new UpdateBookingPolicyCommand(TestBookings.TenantId, 12, 0m, 2, 30, 4, 24),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(existingId, repository.Policy!.Id); // updated in place, not replaced
        Assert.Equal(4, repository.Policy.DefaultPassengerMinimum);
        Assert.Equal(24, repository.Policy.DefaultSeatCapacity);
    }

    [Fact]
    public async Task UpdatePolicy_rejects_invalid_values()
    {
        var repository = new InMemoryBookingPolicyRepository();
        var handler = new UpdateBookingPolicyCommandHandler(repository);

        var negative = await handler.Handle(
            new UpdateBookingPolicyCommand(TestBookings.TenantId, -1, 0m, 2, 30, 3, 7),
            CancellationToken.None);
        Assert.True(negative.IsFailure);
        Assert.Equal(BookingPolicyErrors.NegativeValue, negative.Error);

        var zeroHold = await handler.Handle(
            new UpdateBookingPolicyCommand(TestBookings.TenantId, 12, 0m, 2, 0, 3, 7),
            CancellationToken.None);
        Assert.True(zeroHold.IsFailure);
        Assert.Equal(BookingPolicyErrors.InvalidSeatHold, zeroHold.Error);

        var zeroCapacity = await handler.Handle(
            new UpdateBookingPolicyCommand(TestBookings.TenantId, 12, 0m, 2, 30, 3, 0),
            CancellationToken.None);
        Assert.True(zeroCapacity.IsFailure);
        Assert.Equal(BookingPolicyErrors.InvalidSeatCapacity, zeroCapacity.Error);

        Assert.Equal(0, repository.SaveChangesCallCount);
    }
}
