using NorthernLink.Shared.Kernel;
using NorthernLink.Booking.Domain.BookingDays.Events;

namespace NorthernLink.Booking.Domain.BookingDays;

/// <summary>
/// One corridor's booking state for one service date — the calendar cell. Created lazily
/// by the create-booking command; uniqueness is guaranteed by a DB unique index on
/// (tenant_id, corridor_id, service_date), NOT an application-level existence check
/// (single-API-instance rule: concurrency guarantees must be DB-atomic — the repository
/// catches the unique violation and re-reads). Carries the per-date policy overrides;
/// <see cref="TripId"/> is reserved for the trip created at confirmation in a later batch
/// and stays null in this one. Seat numbers (sold/pending/remaining/needed) are derived
/// in query handlers from the bookings themselves — never stored here.
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

    /// <summary>The trip created at confirmation — always null in this batch.</summary>
    public Guid? TripId { get; private set; }

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
}
