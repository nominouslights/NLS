using NorthernLink.Shared.Kernel;
using NorthernLink.Booking.Domain.Settings.Events;

namespace NorthernLink.Booking.Domain.Settings;

/// <summary>
/// Tenant-wide booking policy — a typed singleton aggregate per tenant (DB unique index on
/// tenant_id), not a key-value bag. Read paths fall back to the <c>Default*</c> constants
/// when no row exists yet, so GET never 404s; the row itself materializes on the first
/// policy write (get-or-create). This batch stores and displays the cancellation window,
/// penalty, and cutoff; hard enforcement of them lands with the public booking PWA batch.
/// Writes are AdminOnly (Owner) — operational pricing policy is not a dispatcher control.
/// </summary>
public sealed class BookingPolicy : AggregateRoot, ITenantScoped
{
    public const int DefaultCancellationWindowHours = 12;
    public const decimal DefaultEarlyCancellationPenaltyCad = 0m;
    public const int DefaultBookingCutoffHours = 2;
    public const int DefaultSeatHoldMinutes = 30;
    public const int DefaultPassengerMinimumValue = 3;
    public const int DefaultSeatCapacityValue = 7;

    private BookingPolicy()
    {
        // EF Core materialization only.
    }

    public Guid TenantId { get; private set; }

    /// <summary>Hours before departure inside which cancelling incurs the penalty.</summary>
    public int CancellationWindowHours { get; private set; }

    /// <summary>Flat penalty (CAD) for cancelling inside the window.</summary>
    public decimal EarlyCancellationPenaltyCad { get; private set; }

    /// <summary>Hours before departure when new bookings close.</summary>
    public int BookingCutoffHours { get; private set; }

    /// <summary>How long an Unconfirmed booking's seats stay reserved.</summary>
    public int SeatHoldMinutes { get; private set; }

    /// <summary>Passengers needed to confirm a day, unless a corridor/day value overrides.</summary>
    public int DefaultPassengerMinimum { get; private set; }

    /// <summary>Seat capacity per day, unless a corridor/day value overrides.</summary>
    public int DefaultSeatCapacity { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>The defaults row, materialized on first write (get-or-create).</summary>
    public static BookingPolicy CreateDefault(Guid tenantId)
    {
        var now = DateTimeOffset.UtcNow;
        var policy = new BookingPolicy
        {
            TenantId = tenantId,
            CancellationWindowHours = DefaultCancellationWindowHours,
            EarlyCancellationPenaltyCad = DefaultEarlyCancellationPenaltyCad,
            BookingCutoffHours = DefaultBookingCutoffHours,
            SeatHoldMinutes = DefaultSeatHoldMinutes,
            DefaultPassengerMinimum = DefaultPassengerMinimumValue,
            DefaultSeatCapacity = DefaultSeatCapacityValue,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        policy.Raise(new BookingPolicyCreatedDomainEvent(policy.Id, tenantId));
        return policy;
    }

    public Result Update(
        int cancellationWindowHours,
        decimal earlyCancellationPenaltyCad,
        int bookingCutoffHours,
        int seatHoldMinutes,
        int defaultPassengerMinimum,
        int defaultSeatCapacity)
    {
        if (cancellationWindowHours < 0 || bookingCutoffHours < 0 || defaultPassengerMinimum < 0)
        {
            return Result.Failure(BookingPolicyErrors.NegativeValue);
        }

        if (earlyCancellationPenaltyCad < 0)
        {
            return Result.Failure(BookingPolicyErrors.NegativeValue);
        }

        if (seatHoldMinutes < 1)
        {
            return Result.Failure(BookingPolicyErrors.InvalidSeatHold);
        }

        if (defaultSeatCapacity < 1)
        {
            return Result.Failure(BookingPolicyErrors.InvalidSeatCapacity);
        }

        CancellationWindowHours = cancellationWindowHours;
        EarlyCancellationPenaltyCad = earlyCancellationPenaltyCad;
        BookingCutoffHours = bookingCutoffHours;
        SeatHoldMinutes = seatHoldMinutes;
        DefaultPassengerMinimum = defaultPassengerMinimum;
        DefaultSeatCapacity = defaultSeatCapacity;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new BookingPolicyUpdatedDomainEvent(Id, TenantId));
        return Result.Success();
    }
}
