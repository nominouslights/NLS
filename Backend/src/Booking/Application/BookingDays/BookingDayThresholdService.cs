using Microsoft.Extensions.Logging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Application.Calendar;
using NorthernLink.Booking.Domain.BookingDays;
using NorthernLink.Booking.Domain.BookingDays.Events;
using NorthernLink.Booking.Domain.Settings;

namespace NorthernLink.Booking.Application.BookingDays;

/// <summary>
/// The unified threshold recompute that runs INSIDE the booking/override transaction. The
/// handler shape is load-bearing and always the same: load the aggregate (clean) →
/// <see cref="EnsureDayAsync"/> → mutate → <see cref="RecomputeAsync"/> → one SaveChanges.
/// <para>
/// <see cref="EnsureDayAsync"/> is the ONLY place a mid-flow save may occur (the
/// repository's DB-atomic get-or-create commits the day row when it has to insert one), so
/// it must run BEFORE anything else is mutated — at that point the tracker holds only clean
/// entities and the early save flushes nothing but the day-create. <see cref="RecomputeAsync"/>
/// never saves: the booking flip, any resulting day transition (with its outbox row — the
/// chain-reaction event Trips turns into the community trip), and the audit entries all
/// commit in the handler's single SaveChanges, which is the DB-atomicity guarantee
/// (single-API-instance rule; the day's optimistic-concurrency Version arbitrates racing
/// recomputes).
/// </para>
/// <para>
/// Because the mutated aggregate is not yet saved, the read-side rows still show its old
/// state; the recompute overlays the in-flight booking's status AND passenger count onto
/// its row first (edits change seat counts too, not just status). Crossing up confirms the
/// day — from Unconfirmed or Reverted. Crossing down while Confirmed applies the window
/// rule: revert only when the minimum is not guaranteed (Gift-a-Seat) and
/// <see cref="CancellationWindow"/> says the departure is still more than the policy window
/// away; inside the window the day stays Confirmed and the run happens. Override changes
/// recompute under exactly the same rules — a raised minimum inside the cancellation window
/// does NOT revert a confirmed day, same as cancellations.
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
    /// Returns the corridor+date's <see cref="BookingDay"/>, creating it when missing
    /// (get-or-create covers legacy bookings from before days materialized eagerly). May
    /// save mid-flow (the insert path), so callers MUST invoke this before mutating any
    /// loaded aggregate — see the class doc.
    /// </summary>
    public Task<BookingDay> EnsureDayAsync(
        Guid tenantId, Guid corridorId, DateOnly serviceDate, CancellationToken cancellationToken = default) =>
        bookingDays.GetOrCreateAsync(
            corridorId,
            serviceDate,
            () => BookingDay.Create(tenantId, corridorId, serviceDate),
            cancellationToken);

    /// <summary>
    /// Recomputes sold-vs-minimum for <paramref name="day"/> and moves its lifecycle in
    /// whichever direction the numbers demand. <paramref name="inFlight"/> is the booking
    /// the current transaction mutated but has not yet saved (null for override changes,
    /// where the read side is already current); its row is overlaid before computing.
    /// Never saves, and the no-op path touches nothing — an untouched tracked day stays
    /// Unchanged, so the eventless-write guard in ModuleDbContext is not tripped.
    /// </summary>
    public async Task RecomputeAsync(
        BookingDay day, Domain.Bookings.Booking? inFlight, CancellationToken cancellationToken = default)
    {
        var math = await ComputeAsync(day, inFlight, cancellationToken);

        if (math.Seats.Sold >= math.Minimum)
        {
            if (day.Status == BookingDayStatus.Confirmed)
            {
                return;
            }

            var confirmed = day.Confirm(await SnapshotAsync(day, inFlight, math, cancellationToken));
            if (confirmed.IsFailure)
            {
                logger.LogWarning(
                    "Booking day {BookingDayId} did not confirm after crossing its minimum: {Error}",
                    day.Id, confirmed.Error.Code);
                return;
            }

            logger.LogInformation(
                "Booking day {BookingDayId} ({CorridorId} {ServiceDate}) confirmed at {Sold}/{Minimum} seats",
                day.Id, day.CorridorId, day.ServiceDate, math.Seats.Sold, math.Minimum);
            return;
        }

        if (day.Status != BookingDayStatus.Confirmed)
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
        if (!CancellationWindow.IsBeforeCutoff(day.ServiceDate, windowHours, now))
        {
            logger.LogInformation(
                "Booking day {BookingDayId} dropped to {Sold}/{Minimum} seats inside the {Window}h window — staying Confirmed, the run happens",
                day.Id, math.Seats.Sold, math.Minimum, windowHours);
            return;
        }

        var recipients = await BuildRecipientsAsync(day, inFlight, cancellationToken);
        var corridorName = await CorridorNameAsync(day, inFlight, cancellationToken);
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
        BookingDay day, Domain.Bookings.Booking? inFlight, CancellationToken cancellationToken)
    {
        var settings = await corridorSettings.GetByCorridorAsync(day.CorridorId, cancellationToken);
        var policy = await policies.GetAsync(cancellationToken);

        var rows = await bookingReads.GetSeatRowsAsync(
            day.CorridorId, day.ServiceDate, day.ServiceDate, cancellationToken);

        // Overlay the in-flight (unsaved) state of the booking this transaction mutated —
        // both status AND passenger count, because the read is a database query that cannot
        // see unsaved passenger edits.
        var overlaid = inFlight is null
            ? rows
            : rows
                .Select(row => row.BookingId == inFlight.Id
                    ? row with { Status = inFlight.Status, PassengerCount = inFlight.Passengers.Count }
                    : row)
                .ToList();

        var capacity = SeatMath.ResolveCapacity(day.SeatCapacityOverride, settings?.SeatCapacity, policy);
        var minimum = SeatMath.ResolveMinimum(day.PassengerMinimumOverride, settings?.PassengerMinimum, policy);
        var seats = SeatMath.Compute(overlaid, clock.GetUtcNow(), capacity, minimum);

        return new DayMath(seats, minimum, capacity, policy);
    }

    private async Task<BookingDayConfirmationSnapshot> SnapshotAsync(
        BookingDay day, Domain.Bookings.Booking? inFlight, DayMath math, CancellationToken cancellationToken)
    {
        var corridor = await corridors.GetAsync(day.CorridorId, cancellationToken);
        return new BookingDayConfirmationSnapshot(
            CorridorName: corridor?.Name ?? inFlight?.CorridorName ?? string.Empty,
            Origin: corridor?.Origin ?? string.Empty,
            Destination: corridor?.Destination ?? string.Empty,
            SeatsSold: math.Seats.Sold,
            SeatsMinimum: math.Minimum,
            SeatCapacity: math.Capacity);
    }

    private async Task<string> CorridorNameAsync(
        BookingDay day, Domain.Bookings.Booking? inFlight, CancellationToken cancellationToken)
    {
        var corridor = await corridors.GetAsync(day.CorridorId, cancellationToken);
        return corridor?.Name ?? inFlight?.CorridorName ?? string.Empty;
    }

    /// <summary>
    /// The revert notification snapshot: one entry per customer holding a non-cancelled
    /// booking on the day (the in-flight booking overlaid first, so a just-cancelled
    /// booking's customer drops out unless another live booking of theirs remains),
    /// customers without an email excluded — they simply cannot be reached this way, which
    /// is not an error.
    /// </summary>
    private async Task<IReadOnlyList<DayRevertRecipient>> BuildRecipientsAsync(
        BookingDay day, Domain.Bookings.Booking? inFlight, CancellationToken cancellationToken)
    {
        var rows = await bookingReads.GetRecipientRowsAsync(
            day.CorridorId, day.ServiceDate, cancellationToken);

        return rows
            .Select(row => inFlight is not null && row.BookingId == inFlight.Id
                ? row with { Status = inFlight.Status }
                : row)
            .Where(row => row.Status != Domain.Bookings.BookingStatus.Cancelled)
            .Where(row => !string.IsNullOrWhiteSpace(row.Email))
            .GroupBy(row => row.CustomerId)
            .Select(group => new DayRevertRecipient(group.First().CustomerName, group.First().Email!.Trim()))
            .ToList();
    }

    private sealed record DayMath(DaySeatSummary Seats, int Minimum, int Capacity, BookingPolicy? Policy);
}
