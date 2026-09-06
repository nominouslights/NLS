using NorthernLink.Shared.Kernel;
using NorthernLink.Booking.Domain.Settings.Events;

namespace NorthernLink.Booking.Domain.Settings;

/// <summary>
/// Per-corridor overrides of the tenant policy's default passenger minimum and seat
/// capacity (e.g. Lynn Lake runs a bigger bus than Leaf Rapids). One row per corridor
/// (DB unique index on tenant_id + corridor_id), upserted by the admin settings screen.
/// Null fields mean "no override — fall through to the policy default"; per-date values
/// on BookingDay override both. Resolution order: day → corridor → policy.
/// </summary>
public sealed class CorridorBookingSettings : AggregateRoot, ITenantScoped
{
    private CorridorBookingSettings()
    {
        // EF Core materialization only.
    }

    public Guid TenantId { get; private set; }

    /// <summary>The Trips route these settings apply to (see corridor_lookup replica).</summary>
    public Guid CorridorId { get; private set; }

    public int? PassengerMinimum { get; private set; }
    public int? SeatCapacity { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<CorridorBookingSettings> Create(
        Guid tenantId,
        Guid corridorId,
        int? passengerMinimum,
        int? seatCapacity)
    {
        if (Validate(passengerMinimum, seatCapacity) is { } error)
        {
            return Result.Failure<CorridorBookingSettings>(error);
        }

        var now = DateTimeOffset.UtcNow;
        var settings = new CorridorBookingSettings
        {
            TenantId = tenantId,
            CorridorId = corridorId,
            PassengerMinimum = passengerMinimum,
            SeatCapacity = seatCapacity,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        settings.Raise(new CorridorBookingSettingsChangedDomainEvent(settings.Id, tenantId));
        return Result.Success(settings);
    }

    public Result Update(int? passengerMinimum, int? seatCapacity)
    {
        if (Validate(passengerMinimum, seatCapacity) is { } error)
        {
            return Result.Failure(error);
        }

        PassengerMinimum = passengerMinimum;
        SeatCapacity = seatCapacity;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new CorridorBookingSettingsChangedDomainEvent(Id, TenantId));
        return Result.Success();
    }

    private static Error? Validate(int? passengerMinimum, int? seatCapacity) =>
        passengerMinimum is < 0 ? BookingPolicyErrors.InvalidCorridorPassengerMinimum
        : seatCapacity is < 1 ? BookingPolicyErrors.InvalidCorridorSeatCapacity
        : null;
}
