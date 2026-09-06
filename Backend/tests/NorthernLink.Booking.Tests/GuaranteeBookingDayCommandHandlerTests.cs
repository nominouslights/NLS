using NorthernLink.Booking.Application.BookingDays.Guarantee;
using NorthernLink.Booking.Domain.BookingDays;
using NorthernLink.Booking.Domain.BookingDays.Events;
using Xunit;

namespace NorthernLink.Booking.Tests;

/// <summary>
/// The Gift-a-Seat endpoint's handler: guarantees the minimum, re-confirms a Reverted day
/// (re-publishing the confirmation event for Trips to absorb), and repeats harmlessly.
/// </summary>
public class GuaranteeBookingDayCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryBookingDayRepository _days = new();
    private readonly FakeBookingReadService _reads = new();
    private readonly InMemoryCorridorSettingsRepository _settings = new();
    private readonly InMemoryBookingPolicyRepository _policies = new();
    private readonly InMemoryCorridorLookupRepository _corridors = new();
    private readonly FakeClock _clock = new(Now);

    private GuaranteeBookingDayCommandHandler Handler =>
        new(_days, _reads, _settings, _policies, _corridors, _clock);

    private BookingDay AddDay(bool confirmed = false, bool reverted = false)
    {
        var day = BookingDay.Create(TestBookings.TenantId, TestBookings.CorridorId, new DateOnly(2026, 9, 15));
        var snapshot = new BookingDayConfirmationSnapshot("Thompson ↔ Lynn Lake", "Thompson", "Lynn Lake", 3, 3, 7);
        if (confirmed || reverted)
        {
            day.Confirm(snapshot);
        }

        if (reverted)
        {
            day.Revert("Thompson ↔ Lynn Lake", 2, 1, [], Now);
        }

        day.ClearDomainEvents();
        _days.Days.Add(day);
        return day;
    }

    [Fact]
    public async Task An_unknown_day_is_not_found()
    {
        var result = await Handler.Handle(
            new GuaranteeBookingDayCommand(TestBookings.TenantId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BookingDayErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Guaranteeing_a_reverted_day_reconfirms_it()
    {
        var day = AddDay(reverted: true);

        var result = await Handler.Handle(
            new GuaranteeBookingDayCommand(TestBookings.TenantId, day.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(day.MinimumGuaranteed);
        Assert.Equal(BookingDayStatus.Confirmed, day.Status);
        Assert.Contains(day.DomainEvents, e => e is BookingDayGuaranteeSetDomainEvent);
        Assert.Contains(day.DomainEvents, e => e is BookingDayConfirmedDomainEvent);
        Assert.Equal(1, _days.SaveChangesCallCount);
    }

    [Fact]
    public async Task Guaranteeing_a_confirmed_day_only_records_the_pledge()
    {
        var day = AddDay(confirmed: true);

        var result = await Handler.Handle(
            new GuaranteeBookingDayCommand(TestBookings.TenantId, day.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(day.MinimumGuaranteed);
        Assert.IsType<BookingDayGuaranteeSetDomainEvent>(Assert.Single(day.DomainEvents));
    }

    [Fact]
    public async Task Repeating_the_guarantee_is_idempotent()
    {
        var day = AddDay(reverted: true);
        await Handler.Handle(
            new GuaranteeBookingDayCommand(TestBookings.TenantId, day.Id), CancellationToken.None);
        day.ClearDomainEvents();

        var result = await Handler.Handle(
            new GuaranteeBookingDayCommand(TestBookings.TenantId, day.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(day.MinimumGuaranteed);
        Assert.Equal(BookingDayStatus.Confirmed, day.Status);
        Assert.Empty(day.DomainEvents);
    }
}
