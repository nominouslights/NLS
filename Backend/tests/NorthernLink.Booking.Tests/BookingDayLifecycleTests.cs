using NorthernLink.Booking.Domain.BookingDays;
using NorthernLink.Booking.Domain.BookingDays.Events;
using Xunit;

namespace NorthernLink.Booking.Tests;

/// <summary>
/// The BookingDay status machine: Unconfirmed → Confirmed → Reverted → Confirmed, the
/// Gift-a-Seat guarantee, and the trip backlink — all at the aggregate level.
/// </summary>
public class BookingDayLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    private static BookingDay Day() =>
        BookingDay.Create(TestBookings.TenantId, TestBookings.CorridorId, new DateOnly(2026, 9, 15));

    private static BookingDayConfirmationSnapshot Snapshot(int sold = 3, int minimum = 3, int capacity = 7) =>
        new("Thompson ↔ Lynn Lake", "Thompson", "Lynn Lake", sold, minimum, capacity);

    [Fact]
    public void A_new_day_starts_unconfirmed_and_unguaranteed()
    {
        var day = Day();

        Assert.Equal(BookingDayStatus.Unconfirmed, day.Status);
        Assert.False(day.MinimumGuaranteed);
        Assert.Null(day.RevertedAtUtc);
        Assert.Null(day.TripId);
        Assert.Null(day.TripNumber);
    }

    [Fact]
    public void Confirm_moves_to_confirmed_and_raises_the_snapshot_event()
    {
        var day = Day();
        day.ClearDomainEvents();

        var result = day.Confirm(Snapshot(sold: 4, minimum: 3, capacity: 7));

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingDayStatus.Confirmed, day.Status);
        var raised = Assert.IsType<BookingDayConfirmedDomainEvent>(Assert.Single(day.DomainEvents));
        Assert.Equal(day.Id, raised.BookingDayId);
        Assert.Equal("Thompson ↔ Lynn Lake", raised.CorridorName);
        Assert.Equal(4, raised.SeatsSold);
        Assert.Equal(3, raised.SeatsMinimum);
        Assert.Equal(7, raised.SeatCapacity);
    }

    [Fact]
    public void Confirming_an_already_confirmed_day_is_a_silent_noop()
    {
        var day = Day();
        day.Confirm(Snapshot());
        day.ClearDomainEvents();

        var result = day.Confirm(Snapshot());

        Assert.True(result.IsSuccess);
        Assert.Empty(day.DomainEvents);
    }

    [Fact]
    public void Revert_requires_confirmed()
    {
        var day = Day();

        var result = day.Revert("Corridor", 2, 1, [], Now);

        Assert.True(result.IsFailure);
        Assert.Equal(BookingDayErrors.NotConfirmed, result.Error);
    }

    [Fact]
    public void Revert_moves_to_reverted_with_the_recipient_snapshot()
    {
        var day = Day();
        day.Confirm(Snapshot());
        day.ClearDomainEvents();

        var recipients = new[] { new DayRevertRecipient("Doris Spence", "doris@example.com") };
        var result = day.Revert("Thompson ↔ Lynn Lake", 2, 1, recipients, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingDayStatus.Reverted, day.Status);
        Assert.Equal(Now, day.RevertedAtUtc);
        var raised = Assert.IsType<BookingDayRevertedDomainEvent>(Assert.Single(day.DomainEvents));
        Assert.Equal(2, raised.SeatsSold);
        Assert.Equal(1, raised.SeatsNeeded);
        Assert.Equal(recipients, raised.Recipients);
    }

    [Fact]
    public void Reverting_an_already_reverted_day_is_a_silent_noop()
    {
        var day = Day();
        day.Confirm(Snapshot());
        day.Revert("Corridor", 2, 1, [], Now);
        day.ClearDomainEvents();

        var result = day.Revert("Corridor", 2, 1, [], Now);

        Assert.True(result.IsSuccess);
        Assert.Empty(day.DomainEvents);
    }

    [Fact]
    public void A_guaranteed_minimum_refuses_to_revert()
    {
        var day = Day();
        day.Confirm(Snapshot());
        day.Guarantee(Snapshot());
        day.ClearDomainEvents();

        var result = day.Revert("Corridor", 0, 3, [], Now);

        Assert.True(result.IsFailure);
        Assert.Equal(BookingDayErrors.MinimumGuaranteedSuppressesRevert, result.Error);
        Assert.Equal(BookingDayStatus.Confirmed, day.Status);
    }

    [Fact]
    public void Guarantee_reconfirms_a_reverted_day()
    {
        var day = Day();
        day.Confirm(Snapshot());
        day.Revert("Corridor", 2, 1, [], Now);
        day.ClearDomainEvents();

        var result = day.Guarantee(Snapshot(sold: 2));

        Assert.True(result.IsSuccess);
        Assert.True(day.MinimumGuaranteed);
        Assert.Equal(BookingDayStatus.Confirmed, day.Status);
        Assert.Null(day.RevertedAtUtc);
        Assert.Contains(day.DomainEvents, e => e is BookingDayGuaranteeSetDomainEvent);
        Assert.Contains(day.DomainEvents, e => e is BookingDayConfirmedDomainEvent);
    }

    [Fact]
    public void Guarantee_is_idempotent()
    {
        var day = Day();
        day.Confirm(Snapshot());
        day.Guarantee(Snapshot());
        day.ClearDomainEvents();

        var result = day.Guarantee(Snapshot());

        Assert.True(result.IsSuccess);
        Assert.Empty(day.DomainEvents);
    }

    [Fact]
    public void Guaranteeing_an_unconfirmed_day_only_records_the_pledge()
    {
        var day = Day();
        day.ClearDomainEvents();

        var result = day.Guarantee(Snapshot(sold: 1));

        Assert.True(result.IsSuccess);
        Assert.True(day.MinimumGuaranteed);
        Assert.Equal(BookingDayStatus.Unconfirmed, day.Status);
        Assert.IsType<BookingDayGuaranteeSetDomainEvent>(Assert.Single(day.DomainEvents));
    }

    [Fact]
    public void Trip_link_is_idempotent_for_the_same_trip_and_a_conflict_for_a_different_one()
    {
        var day = Day();
        var tripId = Guid.NewGuid();
        day.ClearDomainEvents();

        Assert.True(day.LinkTrip(tripId, "NL-1042").IsSuccess);
        Assert.Equal(tripId, day.TripId);
        Assert.Equal("NL-1042", day.TripNumber);
        Assert.IsType<BookingDayTripLinkedDomainEvent>(Assert.Single(day.DomainEvents));
        day.ClearDomainEvents();

        Assert.True(day.LinkTrip(tripId, "NL-1042").IsSuccess);
        Assert.Empty(day.DomainEvents);

        var conflict = day.LinkTrip(Guid.NewGuid(), "NL-9999");
        Assert.True(conflict.IsFailure);
        Assert.Equal(BookingDayErrors.TripAlreadyLinked, conflict.Error);
        Assert.Equal("NL-1042", day.TripNumber);
    }
}
