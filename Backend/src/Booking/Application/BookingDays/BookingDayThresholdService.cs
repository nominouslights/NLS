using Microsoft.Extensions.Logging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Application.Calendar;
using NorthernLink.Booking.Domain.BookingDays;
using NorthernLink.Booking.Domain.BookingDays.Events;
using NorthernLink.Booking.Domain.Settings;

namespace NorthernLink.Booking.Application.BookingDays;

/// <summary>
/// The threshold recompute that runs INSIDE the confirm/cancel booking transaction: after
/// the handler mutates the booking (but before anything is saved), this recomputes the
/// day's sold-vs-minimum and moves the <see cref="BookingDay"/> lifecycle accordingly. It
/// never saves — the calling handler commits booking + day + outbox rows in one
/// SaveChanges, which is the whole DB-atomicity guarantee (single-API-instance rule; the
/// day's optimistic-concurrency Version arbitrates racing recomputes).
/// <para>
/// Because the mutated booking is not yet saved, the read-side rows still show its old
/// status; every computation overlays the in-flight status onto that booking's row first.
/// Crossing up (on a booking confirm) confirms the day — from Unconfirmed or Reverted —
/// publishing the chain-reaction event Trips turns into the community trip. Crossing down
/// (on a cancellation, while Confirmed) applies the window rule: revert only when the
/// minimum is not guaranteed (Gift-a-Seat) and <see cref="CancellationWindow"/> says the
/// departure is still more than the policy window away; inside the window the day stays
/// Confirmed and the run happens.
/// </para>
/// </summary>
public sealed class BookingDayThresholdService(
    IBookingDayRepository bookingDays,
    IBookingReadService bookingReads,
    ICorridorSettingsRepository corridorSettings,
    IBookingPolicyRepository policies,
    ICorridorLookupRepository corridors,
    TimeProvider clock,
    ILogger<BookingDayThresholdService> logger)
{
    /// <summary>
    /// After a booking confirm: crossing (or sitting at/above) the minimum confirms the day.
    /// A confirm never moves a day downward, so nothing else can happen here.
    /// </summary>
    public async Task ApplyAfterConfirmAsync(
        Domain.Bookings.Booking booking, CancellationToken cancellationToken = default)
    {
        // The day normally exists (created with the first booking); get-or-create covers
        // legacy bookings from before days materialized eagerly.
        var day = await bookingDays.GetOrCreateAsync(
            booking.CorridorId,
            booking.ServiceDate,
            () => BookingDay.Create(booking.TenantId, booking.CorridorId, booking.ServiceDate),
            cancellationToken);

        var math = await ComputeAsync(day, booking, cancellationToken);
        if (math.Seats.Sold < math.Minimum || day.Status == BookingDayStatus.Confirmed)
        {
            return;
        }

        var result = day.Confirm(await SnapshotAsync(booking, math, cancellationToken));
        if (result.IsFailure)
        {
            logger.LogWarning(
                "Booking day {BookingDayId} did not confirm after crossing its minimum: {Error}",
                day.Id, result.Error.Code);
            return;
        }

        logger.LogInformation(
            "Booking day {BookingDayId} ({CorridorId} {ServiceDate}) confirmed at {Sold}/{Minimum} seats",
            day.Id, day.CorridorId, day.ServiceDate, math.Seats.Sold, math.Minimum);
    }

    /// <summary>
    /// After a booking cancellation: dropping below the minimum while Confirmed applies the
    /// window rule (revert before the cutoff, stay Confirmed at or past it; a guaranteed
    /// minimum suppresses reverting entirely). A cancellation never moves a day upward.
    /// </summary>
    public async Task ApplyAfterCancelAsync(
        Domain.Bookings.Booking booking, CancellationToken cancellationToken = default)
    {
        var day = await bookingDays.GetAsync(booking.CorridorId, booking.ServiceDate, cancellationToken);
        if (day is null || day.Status != BookingDayStatus.Confirmed)
        {
            return;
        }

        var math = await ComputeAsync(day, booking, cancellationToken);
        if (math.Seats.Sold >= math.Minimum)
        {
            return;
        }

        if (day.MinimumGuaranteed)
        {
            logger.LogInformation(
                "Booking day {BookingDayId} dropped to {Sold}/{Minimum} seats but its minimum is guaranteed — staying Confirmed",
                day.Id, math.Seats.Sold, math.Minimum);
            return;
        }

        var windowHours = math.Policy?.CancellationWindowHours ?? BookingPolicy.DefaultCancellationWindowHours;
        var now = clock.GetUtcNow();
        if (!CancellationWindow.IsBeforeCutoff(booking.ServiceDate, windowHours, now))
        {
            logger.LogInformation(
                "Booking day {BookingDayId} dropped to {Sold}/{Minimum} seats inside the {Window}h window — staying Confirmed, the run happens",
                day.Id, math.Seats.Sold, math.Minimum, windowHours);
            return;
        }

        var recipients = await BuildRecipientsAsync(booking, cancellationToken);
        var corridorName = await CorridorNameAsync(booking, cancellationToken);
        var result = day.Revert(
            corridorName,
            math.Seats.Sold,
            Math.Max(0, math.Minimum - math.Seats.Sold),
            recipients,
            now);

        if (result.IsFailure)
        {
            logger.LogWarning(
                "Booking day {BookingDayId} did not revert after dropping below its minimum: {Error}",
                day.Id, result.Error.Code);
            return;
        }

        logger.LogInformation(
            "Booking day {BookingDayId} ({CorridorId} {ServiceDate}) reverted at {Sold}/{Minimum} seats; {Recipients} customer(s) to notify",
            day.Id, day.CorridorId, day.ServiceDate, math.Seats.Sold, math.Minimum, recipients.Count);
    }

    private async Task<DayMath> ComputeAsync(
        BookingDay day, Domain.Bookings.Booking booking, CancellationToken cancellationToken)
    {
        var settings = await corridorSettings.GetByCorridorAsync(booking.CorridorId, cancellationToken);
        var policy = await policies.GetAsync(cancellationToken);

        var rows = await bookingReads.GetSeatRowsAsync(
            booking.CorridorId, booking.ServiceDate, booking.ServiceDate, cancellationToken);

        // Overlay the in-flight (unsaved) status of the booking this transaction mutated.
        var overlaid = rows
            .Select(row => row.BookingId == booking.Id ? row with { Status = booking.Status } : row)
            .ToList();

        var capacity = SeatMath.ResolveCapacity(day.SeatCapacityOverride, settings?.SeatCapacity, policy);
        var minimum = SeatMath.ResolveMinimum(day.PassengerMinimumOverride, settings?.PassengerMinimum, policy);
        var seats = SeatMath.Compute(overlaid, clock.GetUtcNow(), capacity, minimum);

        return new DayMath(seats, minimum, capacity, policy);
    }

    private async Task<BookingDayConfirmationSnapshot> SnapshotAsync(
        Domain.Bookings.Booking booking, DayMath math, CancellationToken cancellationToken)
    {
        var corridor = await corridors.GetAsync(booking.CorridorId, cancellationToken);
        return new BookingDayConfirmationSnapshot(
            CorridorName: corridor?.Name ?? booking.CorridorName,
            Origin: corridor?.Origin ?? string.Empty,
            Destination: corridor?.Destination ?? string.Empty,
            SeatsSold: math.Seats.Sold,
            SeatsMinimum: math.Minimum,
            SeatCapacity: math.Capacity);
    }

    private async Task<string> CorridorNameAsync(
        Domain.Bookings.Booking booking, CancellationToken cancellationToken)
    {
        var corridor = await corridors.GetAsync(booking.CorridorId, cancellationToken);
        return corridor?.Name ?? booking.CorridorName;
    }

    /// <summary>
    /// The revert notification snapshot: one entry per customer holding a non-cancelled
    /// booking on the day (the just-cancelled booking overlaid first, so its customer drops
    /// out unless another live booking of theirs remains), customers without an email
    /// excluded — they simply cannot be reached this way, which is not an error.
    /// </summary>
    private async Task<IReadOnlyList<DayRevertRecipient>> BuildRecipientsAsync(
        Domain.Bookings.Booking booking, CancellationToken cancellationToken)
    {
        var rows = await bookingReads.GetRecipientRowsAsync(
            booking.CorridorId, booking.ServiceDate, cancellationToken);

        return rows
            .Select(row => row.BookingId == booking.Id ? row with { Status = booking.Status } : row)
            .Where(row => row.Status != Domain.Bookings.BookingStatus.Cancelled)
            .Where(row => !string.IsNullOrWhiteSpace(row.Email))
            .GroupBy(row => row.CustomerId)
            .Select(group => new DayRevertRecipient(group.First().CustomerName, group.First().Email!.Trim()))
            .ToList();
    }

    private sealed record DayMath(DaySeatSummary Seats, int Minimum, int Capacity, BookingPolicy? Policy);
}
