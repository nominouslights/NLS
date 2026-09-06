using NorthernLink.Shared.Kernel;
using NorthernLink.Booking.Domain.BookingDays.Events;

namespace NorthernLink.Booking.Domain.BookingDays;

/// <summary>
/// One corridor's booking state for one service date — the calendar cell. Created lazily
/// by the create-booking command; uniqueness is guaranteed by a DB unique index on
/// (tenant_id, corridor_id, service_date), NOT an application-level existence check
/// (single-API-instance rule: concurrency guarantees must be DB-atomic — the repository
/// catches the unique violation and re-reads). Carries the per-date policy overrides, the
/// <see cref="BookingDayStatus"/> lifecycle (threshold confirmation / revert — see that
/// enum's doc), the Gift-a-Seat <see cref="MinimumGuaranteed"/> pledge, and the
/// <see cref="TripId"/>/<see cref="TripNumber"/> backlink stamped asynchronously when Trips
/// materializes the community trip. Seat numbers (sold/pending/remaining/needed) are derived
/// in query handlers from the bookings themselves — never stored here, which is why the
/// lifecycle methods take caller-computed snapshots for their events.
/// </summary>
public sealed class BookingDay : AggregateRoot, ITenantScoped
{
    private BookingDay()
    {
        // EF Core materialization only.
    }

    public Guid TenantId { get; private set; }
    public Guid CorridorId { get; private set; }
    public DateOnly ServiceDate { get; private set; }

    /// <summary>Per-date passenger-minimum override (resolution: day → corridor → policy).</summary>
    public int? PassengerMinimumOverride { get; private set; }

    /// <summary>Per-date seat-capacity override (resolution: day → corridor → policy).</summary>
    public int? SeatCapacityOverride { get; private set; }

    /// <summary>The trip Trips created at confirmation — null until the backlink event lands.</summary>
    public Guid? TripId { get; private set; }

    /// <summary>Trip-number snapshot alongside <see cref="TripId"/> — display without a join.</summary>
    public string? TripNumber { get; private set; }

    /// <summary>The confirmation/revert lifecycle (see <see cref="BookingDayStatus"/>).</summary>
    public BookingDayStatus Status { get; private set; }

    /// <summary>
    /// Gift-a-Seat: a dispatcher-recorded pledge that the passenger minimum is covered.
    /// While true, dropping below the minimum never reverts the day.
    /// </summary>
    public bool MinimumGuaranteed { get; private set; }

    /// <summary>When the day last reverted; cleared when it re-confirms.</summary>
    public DateTimeOffset? RevertedAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static BookingDay Create(Guid tenantId, Guid corridorId, DateOnly serviceDate)
    {
        var now = DateTimeOffset.UtcNow;
        var day = new BookingDay
        {
            TenantId = tenantId,
            CorridorId = corridorId,
            ServiceDate = serviceDate,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        day.Raise(new BookingDayCreatedDomainEvent(day.Id, tenantId));
        return day;
    }

    public Result SetOverrides(int? passengerMinimum, int? seatCapacity)
    {
        if (passengerMinimum is < 0)
        {
            return Result.Failure(BookingDayErrors.InvalidPassengerMinimum);
        }

        if (seatCapacity is < 1)
        {
            return Result.Failure(BookingDayErrors.InvalidSeatCapacity);
        }

        PassengerMinimumOverride = passengerMinimum;
        SeatCapacityOverride = seatCapacity;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new BookingDayOverridesChangedDomainEvent(Id, TenantId));
        return Result.Success();
    }

    /// <summary>
    /// Confirms the day — the threshold crossed up, or a guarantee re-confirmed a Reverted
    /// day. Idempotent: an already-Confirmed day is a no-op success raising nothing (the
    /// threshold recompute runs on every booking confirm, most of which change nothing).
    /// Raises the mapped confirmation event, which is what makes Trips create the trip —
    /// re-confirming after a revert re-publishes it, and Trips' unique booking-day index
    /// absorbs the duplicate.
    /// </summary>
    public Result Confirm(BookingDayConfirmationSnapshot snapshot)
    {
        if (Status == BookingDayStatus.Confirmed)
        {
            return Result.Success();
        }

        Status = BookingDayStatus.Confirmed;
        RevertedAtUtc = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new BookingDayConfirmedDomainEvent(
            Id,
            TenantId,
            CorridorId,
            snapshot.CorridorName,
            snapshot.Origin,
            snapshot.Destination,
            ServiceDate,
            snapshot.SeatsSold,
            snapshot.SeatsMinimum,
            snapshot.SeatCapacity));
        return Result.Success();
    }

    /// <summary>
    /// Reverts a Confirmed day whose sold seats dropped below the minimum — only reachable
    /// OUTSIDE the cancellation window (the handler owns the clock math) and only while the
    /// minimum is not guaranteed (Gift-a-Seat suppresses reverting entirely; guarded here
    /// again for defence in depth). Idempotent: an already-Reverted day is a no-op success.
    /// Raises the mapped reverted event carrying the caller's recipient snapshot, which is
    /// what makes Notifications send the "trip at risk" emails.
    /// </summary>
    public Result Revert(
        string corridorName,
        int seatsSold,
        int seatsNeeded,
        IReadOnlyList<DayRevertRecipient> recipients,
        DateTimeOffset now)
    {
        if (Status == BookingDayStatus.Reverted)
        {
            return Result.Success();
        }

        if (Status != BookingDayStatus.Confirmed)
        {
            return Result.Failure(BookingDayErrors.NotConfirmed);
        }

        if (MinimumGuaranteed)
        {
            return Result.Failure(BookingDayErrors.MinimumGuaranteedSuppressesRevert);
        }

        Status = BookingDayStatus.Reverted;
        RevertedAtUtc = now;
        UpdatedAtUtc = now;

        Raise(new BookingDayRevertedDomainEvent(
            Id,
            TenantId,
            corridorName,
            ServiceDate,
            seatsSold,
            seatsNeeded,
            recipients,
            TripId,
            TripNumber));
        return Result.Success();
    }

    /// <summary>
    /// Gift-a-Seat: guarantees the day's passenger minimum and — if the day is currently
    /// Reverted — re-confirms it (which re-publishes the confirmation event; Trips absorbs
    /// the duplicate). Idempotent: guaranteeing an already-guaranteed, non-Reverted day is
    /// a no-op success. A day that never confirmed just carries the pledge — it still
    /// confirms through the normal threshold path (which the guarantee then protects).
    /// </summary>
    public Result Guarantee(BookingDayConfirmationSnapshot snapshot)
    {
        var changed = false;

        if (!MinimumGuaranteed)
        {
            MinimumGuaranteed = true;
            changed = true;
            Raise(new BookingDayGuaranteeSetDomainEvent(Id, TenantId));
        }

        if (Status == BookingDayStatus.Reverted)
        {
            var confirmed = Confirm(snapshot);
            if (confirmed.IsFailure)
            {
                return confirmed;
            }

            changed = true;
        }

        if (changed)
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        return Result.Success();
    }

    /// <summary>
    /// Stamps the Trips backlink from <c>trips.trip-scheduled-from-booking</c>. Idempotent:
    /// the same trip re-linking is a no-op success (delivery is at-least-once); a DIFFERENT
    /// trip is a conflict — at most one trip ever exists per booking day, so this indicates
    /// something upstream went badly wrong and must not silently overwrite history.
    /// </summary>
    public Result LinkTrip(Guid tripId, string tripNumber)
    {
        if (TripId == tripId)
        {
            return Result.Success();
        }

        if (TripId is not null)
        {
            return Result.Failure(BookingDayErrors.TripAlreadyLinked);
        }

        if (string.IsNullOrWhiteSpace(tripNumber))
        {
            return Result.Failure(BookingDayErrors.TripNumberRequired);
        }

        TripId = tripId;
        TripNumber = tripNumber.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new BookingDayTripLinkedDomainEvent(Id, TenantId));
        return Result.Success();
    }
}

/// <summary>
/// The caller-computed context a confirmation event needs but the aggregate does not store:
/// the corridor name snapshot (from the corridor replica) and the day's derived seat math
/// at the moment of confirmation.
/// </summary>
public sealed record BookingDayConfirmationSnapshot(
    string CorridorName,
    string Origin,
    string Destination,
    int SeatsSold,
    int SeatsMinimum,
    int SeatCapacity);
