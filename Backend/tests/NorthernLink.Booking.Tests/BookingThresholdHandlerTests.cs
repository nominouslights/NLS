using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Booking.Application.BookingDays;
using NorthernLink.Booking.Application.Bookings.Cancel;
using NorthernLink.Booking.Application.Bookings.Confirm;
using NorthernLink.Booking.Application.Integration;
using NorthernLink.Booking.Domain.BookingDays;
using NorthernLink.Booking.Domain.BookingDays.Events;
using Xunit;
using BookingAggregate = NorthernLink.Booking.Domain.Bookings.Booking;

namespace NorthernLink.Booking.Tests;

/// <summary>
/// The threshold recompute inside the confirm/cancel booking handlers: crossing up
/// confirms the day, crossing down applies the window rule (revert before the cutoff,
/// stay Confirmed inside it, Gift-a-Seat suppresses reverting), and the reverted event's
/// recipient snapshot excludes cancelled bookings and customers without an email.
/// <para>
/// Timeline: service date 2026-09-15 in America/Winnipeg (CDT, UTC−5) departs at local
/// midnight = 2026-09-15T05:00Z; the default 12h window puts the cutoff at
/// 2026-09-14T17:00Z. No policy row is stored, so every threshold comes from the
/// BookingPolicy defaults (minimum 3, window 12h).
/// </para>
/// </summary>
public class BookingThresholdHandlerTests
{
    private static readonly DateOnly ServiceDate = new(2026, 9, 15);
    private static readonly DateTimeOffset Cutoff = new(2026, 9, 14, 17, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WellBeforeCutoff = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryBookingDayRepository _days = new();
    private readonly FakeBookingReadService _reads = new();
    private readonly InMemoryCorridorSettingsRepository _settings = new();
    private readonly InMemoryBookingPolicyRepository _policies = new();
    private readonly InMemoryCorridorLookupRepository _corridors = new();
    private readonly FakeClock _clock = new(WellBeforeCutoff);

    public BookingThresholdHandlerTests()
    {
        _corridors.Corridors.Add(new CorridorLookup
        {
            CorridorId = TestBookings.CorridorId,
            TenantId = TestBookings.TenantId,
            Name = "Thompson ↔ Lynn Lake",
            Origin = "Thompson",
            Destination = "Lynn Lake",
            Active = true,
            UpdatedAtUtc = WellBeforeCutoff,
        });
    }

    private BookingDayThresholdService Thresholds => new(
        _days, _reads, _settings, _policies, _corridors, _clock,
        NullLogger<BookingDayThresholdService>.Instance);

    private ConfirmBookingCommandHandler ConfirmHandler => new(_bookings, Thresholds);

    private CancelBookingCommandHandler CancelHandler => new(_bookings, Thresholds);

    private BookingAggregate AddBooking(int passengers, bool confirmed, Guid? customerId = null, string name = "Doris Spence")
    {
        var booking = TestBookings.Create(passengerCount: passengers, customerId: customerId, customerName: name);
        if (confirmed)
        {
            booking.Confirm();
        }

        booking.ClearDomainEvents();
        _bookings.Bookings.Add(booking);
        _reads.Bookings.Add(booking);
        return booking;
    }

    private BookingDay AddDay(bool confirmed = false, bool guaranteed = false)
    {
        var day = BookingDay.Create(TestBookings.TenantId, TestBookings.CorridorId, ServiceDate);
        var snapshot = new BookingDayConfirmationSnapshot("Thompson ↔ Lynn Lake", "Thompson", "Lynn Lake", 3, 3, 7);
        if (confirmed)
        {
            day.Confirm(snapshot);
        }

        if (guaranteed)
        {
            day.Guarantee(snapshot);
        }

        day.ClearDomainEvents();
        _days.Days.Add(day);
        return day;
    }

    [Fact]
    public async Task Confirming_the_booking_that_reaches_the_minimum_confirms_the_day()
    {
        AddBooking(passengers: 2, confirmed: true);
        var crossing = AddBooking(passengers: 1, confirmed: false, customerId: Guid.NewGuid(), name: "Levi Park");
        var day = AddDay();

        var result = await ConfirmHandler.Handle(
            new ConfirmBookingCommand(TestBookings.TenantId, crossing.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingDayStatus.Confirmed, day.Status);
        var raised = Assert.IsType<BookingDayConfirmedDomainEvent>(Assert.Single(day.DomainEvents));
        Assert.Equal(3, raised.SeatsSold);
        Assert.Equal(3, raised.SeatsMinimum);
        Assert.Equal("Thompson ↔ Lynn Lake", raised.CorridorName);
        Assert.Equal(1, _bookings.SaveChangesCallCount);
    }

    [Fact]
    public async Task Confirming_below_the_minimum_leaves_the_day_unconfirmed()
    {
        var below = AddBooking(passengers: 2, confirmed: false);
        var day = AddDay();

        var result = await ConfirmHandler.Handle(
            new ConfirmBookingCommand(TestBookings.TenantId, below.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingDayStatus.Unconfirmed, day.Status);
        Assert.Empty(day.DomainEvents);
    }

    [Fact]
    public async Task Confirming_while_the_day_is_already_confirmed_raises_nothing_new()
    {
        AddBooking(passengers: 3, confirmed: true);
        var extra = AddBooking(passengers: 1, confirmed: false, customerId: Guid.NewGuid(), name: "Levi Park");
        var day = AddDay(confirmed: true);

        var result = await ConfirmHandler.Handle(
            new ConfirmBookingCommand(TestBookings.TenantId, extra.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingDayStatus.Confirmed, day.Status);
        Assert.Empty(day.DomainEvents);
    }

    [Fact]
    public async Task Cancelling_below_the_minimum_before_the_cutoff_reverts_the_day()
    {
        AddBooking(passengers: 2, confirmed: true);
        var cancelled = AddBooking(passengers: 1, confirmed: true, customerId: Guid.NewGuid(), name: "Levi Park");
        var day = AddDay(confirmed: true);
        _clock.UtcNow = Cutoff.AddHours(-1);

        var result = await CancelHandler.Handle(
            new CancelBookingCommand(TestBookings.TenantId, cancelled.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingDayStatus.Reverted, day.Status);
        Assert.Equal(_clock.UtcNow, day.RevertedAtUtc);
        var raised = Assert.IsType<BookingDayRevertedDomainEvent>(Assert.Single(day.DomainEvents));
        Assert.Equal(2, raised.SeatsSold);
        Assert.Equal(1, raised.SeatsNeeded);
    }

    [Fact]
    public async Task Cancelling_one_second_before_the_cutoff_still_reverts()
    {
        AddBooking(passengers: 2, confirmed: true);
        var cancelled = AddBooking(passengers: 1, confirmed: true, customerId: Guid.NewGuid(), name: "Levi Park");
        var day = AddDay(confirmed: true);
        _clock.UtcNow = Cutoff.AddSeconds(-1);

        await CancelHandler.Handle(
            new CancelBookingCommand(TestBookings.TenantId, cancelled.Id), CancellationToken.None);

        Assert.Equal(BookingDayStatus.Reverted, day.Status);
    }

    [Fact]
    public async Task Cancelling_exactly_at_the_cutoff_stays_confirmed()
    {
        AddBooking(passengers: 2, confirmed: true);
        var cancelled = AddBooking(passengers: 1, confirmed: true, customerId: Guid.NewGuid(), name: "Levi Park");
        var day = AddDay(confirmed: true);
        _clock.UtcNow = Cutoff;

        var result = await CancelHandler.Handle(
            new CancelBookingCommand(TestBookings.TenantId, cancelled.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingDayStatus.Confirmed, day.Status);
        Assert.Empty(day.DomainEvents);
    }

    [Fact]
    public async Task Cancelling_inside_the_window_stays_confirmed_and_the_run_happens()
    {
        AddBooking(passengers: 2, confirmed: true);
        var cancelled = AddBooking(passengers: 1, confirmed: true, customerId: Guid.NewGuid(), name: "Levi Park");
        var day = AddDay(confirmed: true);
        _clock.UtcNow = Cutoff.AddHours(3);

        await CancelHandler.Handle(
            new CancelBookingCommand(TestBookings.TenantId, cancelled.Id), CancellationToken.None);

        Assert.Equal(BookingDayStatus.Confirmed, day.Status);
        Assert.Empty(day.DomainEvents);
    }

    [Fact]
    public async Task A_guaranteed_minimum_suppresses_the_revert()
    {
        AddBooking(passengers: 2, confirmed: true);
        var cancelled = AddBooking(passengers: 1, confirmed: true, customerId: Guid.NewGuid(), name: "Levi Park");
        var day = AddDay(confirmed: true, guaranteed: true);
        _clock.UtcNow = WellBeforeCutoff;

        var result = await CancelHandler.Handle(
            new CancelBookingCommand(TestBookings.TenantId, cancelled.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingDayStatus.Confirmed, day.Status);
        Assert.Empty(day.DomainEvents);
    }

    [Fact]
    public async Task Cancelling_while_still_at_or_above_the_minimum_changes_nothing()
    {
        AddBooking(passengers: 3, confirmed: true);
        var cancelled = AddBooking(passengers: 1, confirmed: true, customerId: Guid.NewGuid(), name: "Levi Park");
        var day = AddDay(confirmed: true);

        await CancelHandler.Handle(
            new CancelBookingCommand(TestBookings.TenantId, cancelled.Id), CancellationToken.None);

        Assert.Equal(BookingDayStatus.Confirmed, day.Status);
        Assert.Empty(day.DomainEvents);
    }

    [Fact]
    public async Task The_revert_recipient_snapshot_excludes_cancelled_bookings_and_customers_without_email()
    {
        var doris = Guid.NewGuid();
        var levi = Guid.NewGuid();
        var noEmail = Guid.NewGuid();

        AddBooking(passengers: 1, confirmed: true, customerId: doris, name: "Doris Spence");
        AddBooking(passengers: 1, confirmed: true, customerId: noEmail, name: "Walk-up Customer");
        var cancelled = AddBooking(passengers: 1, confirmed: true, customerId: levi, name: "Levi Park");
        var day = AddDay(confirmed: true);

        _reads.EmailsByCustomerId[doris] = "doris@example.com";
        _reads.EmailsByCustomerId[levi] = "levi@example.com";
        _reads.EmailsByCustomerId[noEmail] = null;

        await CancelHandler.Handle(
            new CancelBookingCommand(TestBookings.TenantId, cancelled.Id), CancellationToken.None);

        Assert.Equal(BookingDayStatus.Reverted, day.Status);
        var raised = Assert.IsType<BookingDayRevertedDomainEvent>(Assert.Single(day.DomainEvents));
        var recipient = Assert.Single(raised.Recipients);
        Assert.Equal("Doris Spence", recipient.Name);
        Assert.Equal("doris@example.com", recipient.Email);
    }

    [Fact]
    public async Task A_customer_with_another_live_booking_stays_in_the_snapshot()
    {
        var doris = Guid.NewGuid();
        AddBooking(passengers: 2, confirmed: true, customerId: doris, name: "Doris Spence");
        var cancelled = AddBooking(passengers: 1, confirmed: true, customerId: doris, name: "Doris Spence");
        var day = AddDay(confirmed: true);
        _reads.EmailsByCustomerId[doris] = "doris@example.com";

        await CancelHandler.Handle(
            new CancelBookingCommand(TestBookings.TenantId, cancelled.Id), CancellationToken.None);

        Assert.Equal(BookingDayStatus.Reverted, day.Status);
        var raised = Assert.IsType<BookingDayRevertedDomainEvent>(Assert.Single(day.DomainEvents));
        var recipient = Assert.Single(raised.Recipients);
        Assert.Equal("doris@example.com", recipient.Email);
    }
}
